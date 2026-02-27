using System.Xml.Linq;
using DataManager.Core.Abstractions;
using DataManager.Core.Models.Dacpac;
using DataManager.Infrastructure.Dacpac;
using DataManager.Core.Utilities;
using DataManager.Core.Models;

namespace DataManager.Infrastructure.Generation;

/// <summary>
/// Orchestrates the end-to-end entity generation pipeline:
/// Excel → DACPAC extraction → XML parsing → code generation → file writing → report writing.
/// </summary>
public class GenerationOrchestrator
{
    private readonly ExcelReaderService          _excelReader;
    private readonly DacpacExtractorService      _dacpacExtractor;
    private readonly ModelXmlParserService       _modelXmlParser;
    private readonly PrimaryKeyEnricher          _pkEnricher;
    private readonly EntityClassGenerator        _entityGenerator;
    private readonly EntityConfigurationGenerator _configGenerator;
    private readonly FileWriterService           _fileWriter;
    private readonly DbContextGenerator          _dbContextGenerator;
    private readonly IGenerationLogger           _logger;

    public GenerationOrchestrator(
        ExcelReaderService           excelReader,
        DacpacExtractorService       dacpacExtractor,
        ModelXmlParserService        modelXmlParser,
        PrimaryKeyEnricher           pkEnricher,
        EntityClassGenerator         entityGenerator,
        EntityConfigurationGenerator configGenerator,
        FileWriterService            fileWriter,
        DbContextGenerator           dbContextGenerator,
        IGenerationLogger            logger)
    {
        _excelReader         = excelReader;
        _dacpacExtractor     = dacpacExtractor;
        _modelXmlParser      = modelXmlParser;
        _pkEnricher          = pkEnricher;
        _entityGenerator     = entityGenerator;
        _configGenerator     = configGenerator;
        _fileWriter          = fileWriter;
        _dbContextGenerator  = dbContextGenerator;
        _logger              = logger;
    }

    /// <summary>
    /// Runs the full generation pipeline and returns a summary of what was produced.
    /// </summary>
    /// <param name="inputDirectory">Directory containing the Excel file and DACPAC sub-folder.</param>
    /// <param name="sqlEntityAndConfigOutputDir">Folder for entity .cs files and SQL Server configuration files.</param>
    /// <param name="sqlDbContextFilePath">Full path to the SQL Server DbContext .cs file to write.</param>
    /// <param name="sqliteConfigOutputDir">Folder for SQLite configuration files.</param>
    /// <param name="sqliteDbContextFilePath">Full path to the SQLite DbContext .cs file to write.</param>
    public GenerationResult Run(
        string inputDirectory,
        string sqlEntityAndConfigOutputDir,
        string sqlDbContextFilePath,
        string sqliteConfigOutputDir,
        string sqliteDbContextFilePath)
    {
        var result = new GenerationResult { Success = true };

        // Ensure a clean SQL entity/config output directory (input staging is separate)
        _fileWriter.CleanOutputDirectory(sqlEntityAndConfigOutputDir, force: true);
        _fileWriter.EnsureOutputDirectoryExists(sqlEntityAndConfigOutputDir);
        _fileWriter.EnsureOutputDirectoryExists(sqliteConfigOutputDir);

        // ── Step 1: Locate Excel file ─────────────────────────────────────────
        var excelFilePath = _excelReader.FindExcelFile(inputDirectory);
        if (excelFilePath == null)
        {
            _logger.LogError("No Excel file found. Please place an .xlsx file in the _input/ folder.");
            result.Success = false;
            return result;
        }

        // ── Step 2: Read & filter rows ────────────────────────────────────────
        var filteredRows = _excelReader.ReadAndFilterExcel(excelFilePath);
        if (filteredRows.Count == 0)
        {
            _logger.LogWarning("No rows matched the filter criteria.");
            return result;
        }

        _logger.LogInfo("");

        // ── Step 3: Group by Server / Database ────────────────────────────────
        var groupedData = _excelReader.GroupByServerAndDatabase(filteredRows);

        var allTableDefinitions  = new List<TableDefinition>();
        var allViews             = new List<ViewDefinition>();
        var allDiscoveryReports  = new List<ElementDiscoveryReport>();

        // ── Step 4: Process each Server/Database combination ─────────────────
        foreach (var serverGroup in groupedData)
        {
            var server = serverGroup.Key;

            foreach (var databaseGroup in serverGroup.Value)
            {
                var database = databaseGroup.Key;
                var rows     = databaseGroup.Value;

                _logger.LogInfo($"Processing Server: {server}, Database: {database}");

                // Check DACPAC exists
                if (!_dacpacExtractor.DacpacExists(inputDirectory, server, database))
                {
                    var errorMsg = $"DACPAC file not found: {server}_{database}.dacpac";
                    _logger.LogError(errorMsg);
                    result.Errors.Add(errorMsg);
                    result.ErrorsEncountered++;
                    continue;
                }

                // Extract model.xml
                var modelXml = _dacpacExtractor.ExtractModelXml(inputDirectory, server, database);
                if (modelXml == null)
                {
                    var errorMsg = $"Failed to extract model.xml from DACPAC: {server}_{database}.dacpac";
                    result.Errors.Add(errorMsg);
                    result.ErrorsEncountered++;
                    continue;
                }

                // Parse and validate the model XML once for this database
                var doc = _modelXmlParser.PrepareDocument(modelXml, server, database);
                if (doc == null)
                {
                    var errorMsg = $"Failed to parse or validate model.xml from DACPAC: {server}_{database}.dacpac";
                    result.Errors.Add(errorMsg);
                    result.ErrorsEncountered++;
                    continue;
                }

                // Discovery report
                var discoveryReport = _modelXmlParser.GenerateDiscoveryReport(doc, server, database);
                allDiscoveryReports.Add(discoveryReport);
                _logger.LogInfo(
                    $"[{server}].[{database}] - Discovery: " +
                    $"{discoveryReport.StoredProcedures.Count} stored procedures, " +
                    $"{discoveryReport.Sequences.Count} sequences, " +
                    $"{discoveryReport.Triggers.Count} triggers");

                // Parse and write view entities
                _logger.LogInfo($"[{server}].[{database}] - Parsing views from DACPAC");
                var views = _modelXmlParser.ParseViews(doc, server, database);

                foreach (var view in views)
                {
                    var viewCode = _entityGenerator.GenerateViewClass(view);
                    if (_fileWriter.WriteViewFile(sqlEntityAndConfigOutputDir, server, database, view.Schema, view.ViewName, viewCode))
                    {
                        result.ViewsGenerated++;
                        allViews.Add(view);
                    }
                    else
                    {
                        var errorMsg = $"Failed to write view file: [{server}].[{database}].[{view.Schema}].[{view.ViewName}]";
                        result.Errors.Add(errorMsg);
                        result.ErrorsEncountered++;
                    }
                }

                if (views.Count > 0)
                    _logger.LogProgress($"[{server}].[{database}] - Generated {views.Count} view entities");

                // Process tables
                var tableGroups = rows
                    .GroupBy(r => new { r.Schema, r.Table })
                    .Where(g => g.Any(r => r.Generate))
                    .ToList();

                _logger.LogInfo($"Found {tableGroups.Count} tables to process");
                _logger.LogInfo("");

                foreach (var tableGroup in tableGroups)
                {
                    var schema          = tableGroup.Key.Schema;
                    var tableName       = tableGroup.Key.Table;
                    var requiredColumns = tableGroup.Select(r => r.Column).Distinct().ToList();

                    var tableDefinition = _modelXmlParser.ParseTable(doc, server, database, schema, tableName, requiredColumns);
                    if (tableDefinition == null)
                    {
                        result.TablesSkipped++;
                        continue;
                    }

                    if (!_pkEnricher.EnrichTableWithPrimaryKeys(tableDefinition))
                    {
                        result.TablesSkipped++;
                        continue;
                    }

                    if (!_entityGenerator.ValidateEntityClass(tableDefinition))
                    {
                        var errorMsg = $"Failed validation for entity class: [{server}].[{database}].[{schema}].[{tableName}]";
                        result.Errors.Add(errorMsg);
                        result.TablesSkipped++;
                        result.ErrorsEncountered++;
                        continue;
                    }

                    var entityCode = _entityGenerator.GenerateEntityClass(tableDefinition);
                    if (_fileWriter.WriteEntityFile(sqlEntityAndConfigOutputDir, server, database, schema, tableName, entityCode))
                    {
                        result.EntitiesGenerated++;
                    }
                    else
                    {
                        var errorMsg = $"Failed to write entity file: [{server}].[{database}].[{schema}].[{tableName}]";
                        result.Errors.Add(errorMsg);
                        result.ErrorsEncountered++;
                    }

                    allTableDefinitions.Add(tableDefinition);
                }

                _logger.LogInfo("");
            }
        }

        // ── Step 5: Generate configuration classes ────────────────────────────
        if (allTableDefinitions.Count > 0 || allViews.Count > 0)
        {
            _logger.LogInfo("Generating entity configurations...");

            var tablesByDb = allTableDefinitions
                .GroupBy(t => new { t.Server, t.Database })
                .ToDictionary(g => g.Key, g => g.ToList());

            var viewsByDb = allViews
                .GroupBy(v => new { v.Server, v.Database })
                .ToDictionary(g => g.Key, g => g.ToList());

            var serverDatabasePairs = new List<(string Server, string Database)>();

            var allKeys = tablesByDb.Keys
                .Union(viewsByDb.Keys)
                .Distinct()
                .ToList();

            foreach (var key in allKeys)
            {
                var server   = key.Server;
                var database = key.Database;
                serverDatabasePairs.Add((server, database));

                tablesByDb.TryGetValue(key, out var tables);
                viewsByDb.TryGetValue(key, out var views);

                tables ??= new List<TableDefinition>();
                views  ??= new List<ViewDefinition>();

                var configCode = _configGenerator.GenerateCombinedSQLConfiguration(server, database, tables, views);

                if (!_fileWriter.WriteConfigurationFile(sqlEntityAndConfigOutputDir, server, database, configCode))
                {
                    var errorMsg = $"Failed to write SQL configuration file: [{server}].[{database}]";
                    result.Errors.Add(errorMsg);
                    result.ErrorsEncountered++;
                }

                // SQLite configuration
                _logger.LogInfo($"[{server}].[{database}] - Generating SQLite configuration...");
                var sqliteConfigCode = _configGenerator.GenerateCombinedSQLiteConfiguration(server, database, tables ?? new(), views ?? new());
                if (!_fileWriter.WriteSQLiteConfigurationFile(sqliteConfigOutputDir, server, database, sqliteConfigCode))
                {
                    var errorMsg = $"Failed to write SQLite configuration file: [{server}].[{database}]";
                    result.Errors.Add(errorMsg);
                    result.ErrorsEncountered++;
                }
            }

            // ── Step 6: Generate SQLDbContext ─────────────────────────────────
            _logger.LogInfo("");
            _logger.LogInfo("Generating SQLDbContext...");
            var dbContextCode = _dbContextGenerator.GenerateSQLDbContext(allTableDefinitions, allViews, serverDatabasePairs);
            if (!_fileWriter.WriteToPath(sqlDbContextFilePath, dbContextCode))
            {
                var errorMsg = "Failed to write SQLDbContext file";
                result.Errors.Add(errorMsg);
                result.ErrorsEncountered++;
            }

            // ── Step 6b: Generate SQLiteDbContext ─────────────────────────────
            _logger.LogInfo("");
            _logger.LogInfo("Generating SQLiteDbContext...");
            var sqliteDbContextCode = _dbContextGenerator.GenerateSQLiteDbContext(allTableDefinitions, allViews, serverDatabasePairs);
            if (!_fileWriter.WriteToPath(sqliteDbContextFilePath, sqliteDbContextCode))
            {
                var errorMsg = "Failed to write SQLiteDbContext file";
                result.Errors.Add(errorMsg);
                result.ErrorsEncountered++;
            }
        }

        result.DiscoveryReports = allDiscoveryReports;
        return result;
    }
}
