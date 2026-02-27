using Catalogue.Core.Abstractions;
using Catalogue.Core.Models.Dacpac;

namespace Catalogue.Infrastructure.Generation;

/// <summary>
/// Orchestrates entity generation sourced from CatalogueDb (via an
/// <see cref="ISchemaDataSource"/>) rather than from Excel + DACPAC files.
/// The file-based <see cref="GenerationOrchestrator"/> remains for the legacy
/// console pipeline.
/// </summary>
public class CatalogueGenerationOrchestrator
{
    private readonly EntityClassGenerator         _entityGenerator;
    private readonly EntityConfigurationGenerator _configGenerator;
    private readonly FileWriterService            _fileWriter;
    private readonly DbContextGenerator           _dbContextGenerator;
    private readonly IGenerationLogger            _logger;

    public CatalogueGenerationOrchestrator(
        EntityClassGenerator         entityGenerator,
        EntityConfigurationGenerator configGenerator,
        FileWriterService            fileWriter,
        DbContextGenerator           dbContextGenerator,
        IGenerationLogger            logger)
    {
        _entityGenerator    = entityGenerator;
        _configGenerator    = configGenerator;
        _fileWriter         = fileWriter;
        _dbContextGenerator = dbContextGenerator;
        _logger             = logger;
    }

    /// <summary>
    /// Runs the generation pipeline against the provided data source and writes
    /// output files to the specified paths.
    /// </summary>
    /// <param name="dataSource">Configured data source (e.g. <c>CatalogueDbSchemaDataSource</c>).</param>
    /// <param name="sqlEntityAndConfigOutputDir">Folder for entity .cs files and SQL Server configuration files.</param>
    /// <param name="sqlDbContextFilePath">Full path to the SQL Server DbContext .cs file to write.</param>
    /// <param name="sqliteConfigOutputDir">Folder for SQLite configuration files.</param>
    /// <param name="sqliteDbContextFilePath">Full path to the SQLite DbContext .cs file to write.</param>
    public async Task<GenerationResult> RunAsync(
        ISchemaDataSource dataSource,
        string sqlEntityAndConfigOutputDir,
        string sqlDbContextFilePath,
        string sqliteConfigOutputDir,
        string sqliteDbContextFilePath)
    {
        var result = new GenerationResult { Success = true };

        // Ensure clean output directories
        _fileWriter.CleanOutputDirectory(sqlEntityAndConfigOutputDir, force: true);
        _fileWriter.EnsureOutputDirectoryExists(sqlEntityAndConfigOutputDir);
        _fileWriter.EnsureOutputDirectoryExists(sqliteConfigOutputDir);

        // ── Step 1: Load tables from data source ──────────────────────────────
        _logger.LogInfo("Loading tables from CatalogueDb...");
        var tables = await dataSource.GetTablesForGenerationAsync();
        _logger.LogInfo($"Found {tables.Count} table(s) with selected columns.");

        // ── Step 2: Load views from data source ───────────────────────────────
        _logger.LogInfo("Loading views from CatalogueDb...");
        var views = await dataSource.GetViewsAsync();
        _logger.LogInfo($"Found {views.Count} view(s).");

        // ── Step 3: Collect discovery report ─────────────────────────────────
        var discoveryReport = await dataSource.GetDiscoveryReportAsync();
        result.DiscoveryReports.Add(discoveryReport);

        if (tables.Count == 0 && views.Count == 0)
        {
            _logger.LogWarning("No tables or views found for generation. " +
                               "Ensure columns are marked IsSelectedForLoad = true and PersistenceType ≠ 'D'.");
            return result;
        }

        _logger.LogInfo(string.Empty);

        // ── Step 4: Generate entity classes ───────────────────────────────────
        _logger.LogInfo("Generating entity classes...");
        var generatedTables = new List<TableDefinition>();

        foreach (var table in tables)
        {
            if (!_entityGenerator.ValidateEntityClass(table))
            {
                var msg = $"Validation failed: [{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}]";
                _logger.LogError(msg);
                result.Errors.Add(msg);
                result.TablesSkipped++;
                result.ErrorsEncountered++;
                continue;
            }

            var entityCode = _entityGenerator.GenerateEntityClass(table);
            if (_fileWriter.WriteEntityFile(
                    sqlEntityAndConfigOutputDir, table.Server, table.Database, table.Schema, table.TableName, entityCode))
            {
                result.EntitiesGenerated++;
                generatedTables.Add(table);
            }
            else
            {
                var msg = $"Failed to write entity: [{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}]";
                result.Errors.Add(msg);
                result.ErrorsEncountered++;
            }
        }

        // ── Step 5: Generate view classes ─────────────────────────────────────
        var generatedViews = new List<ViewDefinition>();

        foreach (var view in views)
        {
            var viewCode = _entityGenerator.GenerateViewClass(view);
            if (_fileWriter.WriteViewFile(
                    sqlEntityAndConfigOutputDir, view.Server, view.Database, view.Schema, view.ViewName, viewCode))
            {
                result.ViewsGenerated++;
                generatedViews.Add(view);
            }
            else
            {
                var msg = $"Failed to write view: [{view.Server}].[{view.Database}].[{view.Schema}].[{view.ViewName}]";
                result.Errors.Add(msg);
                result.ErrorsEncountered++;
            }
        }

        if (generatedTables.Count > 0 || generatedViews.Count > 0)
        {
            _logger.LogInfo(string.Empty);

            // ── Step 6: Generate combined entity-configuration classes ─────────
            _logger.LogInfo("Generating entity configurations...");

            var tablesByDb = generatedTables
                .GroupBy(t => new { t.Server, t.Database })
                .ToDictionary(g => g.Key, g => g.ToList());

            var viewsByDb = generatedViews
                .GroupBy(v => new { v.Server, v.Database })
                .ToDictionary(g => g.Key, g => g.ToList());

            var allKeys = tablesByDb.Keys.Union(viewsByDb.Keys).Distinct().ToList();
            var serverDatabasePairs = new List<(string Server, string Database)>();

            foreach (var key in allKeys)
            {
                var server   = key.Server;
                var database = key.Database;
                serverDatabasePairs.Add((server, database));

                tablesByDb.TryGetValue(key, out var dbTables);
                viewsByDb.TryGetValue(key, out var dbViews);
                dbTables ??= new List<TableDefinition>();
                dbViews  ??= new List<ViewDefinition>();

                var configCode = _configGenerator.GenerateCombinedSQLConfiguration(server, database, dbTables, dbViews);
                if (!_fileWriter.WriteConfigurationFile(sqlEntityAndConfigOutputDir, server, database, configCode))
                {
                    var msg = $"Failed to write SQL configuration: [{server}].[{database}]";
                    result.Errors.Add(msg);
                    result.ErrorsEncountered++;
                }

                // ── Step 6b: Generate SQLite configuration ─────────────────────────
                _logger.LogInfo(string.Empty);
                _logger.LogInfo($"Generating SQLite configuration for [{server}].[{database}]...");
                var sqliteConfigCode = _configGenerator.GenerateCombinedSQLiteConfiguration(server, database, dbTables, dbViews);
                if (!_fileWriter.WriteSQLiteConfigurationFile(sqliteConfigOutputDir, server, database, sqliteConfigCode))
                {
                    var msg = $"Failed to write SQLite configuration: [{server}].[{database}]";
                    result.Errors.Add(msg);
                    result.ErrorsEncountered++;
                }
            }

            // ── Step 7: Generate SQLDbContext ─────────────────────────────────
            _logger.LogInfo(string.Empty);
            _logger.LogInfo("Generating SQLDbContext...");
            var dbContextCode = _dbContextGenerator.GenerateSQLDbContext(
                generatedTables, generatedViews, serverDatabasePairs);

            if (!_fileWriter.WriteToPath(sqlDbContextFilePath, dbContextCode))
            {
                var msg = "Failed to write SQLDbContext file";
                result.Errors.Add(msg);
                result.ErrorsEncountered++;
            }

            // ── Step 7b: Generate SQLiteDbContext ─────────────────────────────
            _logger.LogInfo(string.Empty);
            _logger.LogInfo("Generating SQLiteDbContext...");
            var sqliteDbContextCode = _dbContextGenerator.GenerateSQLiteDbContext(
                generatedTables, generatedViews, serverDatabasePairs);

            if (!_fileWriter.WriteToPath(sqliteDbContextFilePath, sqliteDbContextCode))
            {
                var msg = "Failed to write SQLiteDbContext file";
                result.Errors.Add(msg);
                result.ErrorsEncountered++;
            }
        }

        _logger.LogInfo(string.Empty);
        _logger.LogProgress(
            $"Generation complete: {result.EntitiesGenerated} entit{(result.EntitiesGenerated == 1 ? "y" : "ies")}, " +
            $"{result.ViewsGenerated} view{(result.ViewsGenerated == 1 ? "" : "s")}" +
            (result.ErrorsEncountered > 0 ? $", {result.ErrorsEncountered} error(s)." : "."));

        return result;
    }
}
