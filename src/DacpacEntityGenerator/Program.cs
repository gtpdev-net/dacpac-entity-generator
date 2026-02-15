using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Services;
using DacpacEntityGenerator.Utilities;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Reflection.Emit;

namespace DacpacEntityGenerator;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            ConsoleLogger.LogInfo("=== DACPAC to Entity Class Generator ===");
            ConsoleLogger.LogInfo("Starting entity generation process...");
            ConsoleLogger.LogInfo("");

            // Define input and output directories
            // Look for input/output at workspace root (go up from bin/Debug/net8.0 or from src/DacpacEntityGenerator)
            var currentDir = Directory.GetCurrentDirectory();
            var workspaceRoot = currentDir;
            
            // If running from bin directory, navigate up to workspace root
            if (currentDir.Contains("bin"))
            {
                workspaceRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));
            }

            var projectDirectory = Path.Combine(workspaceRoot, "src\\DataLayer.DacpacEntityGenerator");
            var inputDirectory = Path.Combine(projectDirectory, "_input");
            var outputDirectory = Path.Combine(projectDirectory, "_output");

            // Purge output directory
            if (Directory.Exists(outputDirectory))
            {
                foreach (var file in Directory.GetFiles(outputDirectory))
                {
                    File.Delete(file);
                }
                foreach (var dir in Directory.GetDirectories(outputDirectory))
                {
                    Directory.Delete(dir, true);
                }
            }

            // Initialize services
            var excelReader = new ExcelReaderService();
            var dacpacExtractor = new DacpacExtractorService();
            var modelXmlParser = new ModelXmlParserService();
            var pkEnricher = new PrimaryKeyEnricher();
            var entityGenerator = new EntityClassGenerator();
            var fileWriter = new FileWriterService();

            // Ensure output directory exists
            fileWriter.EnsureOutputDirectoryExists(outputDirectory);


            // Step 1: Find and read Excel file
            var excelFilePath = excelReader.FindExcelFile(inputDirectory);
            if (excelFilePath == null)
            {
                ConsoleLogger.LogError("No Excel file found. Please place an .xlsx file in the ./input/ folder.");
                Environment.Exit(1);
            }

            // Step 2: Read and filter Excel data
            var filteredRows = excelReader.ReadAndFilterExcel(excelFilePath);
            if (filteredRows.Count == 0)
            {
                ConsoleLogger.LogWarning("No rows matched the filter criteria (Table in DAO Analysis = TRUE, Persistence Type = 'R')");
                Environment.Exit(0);
            }

            ConsoleLogger.LogInfo("");

            // Step 3: Group by Server and Database
            var groupedData = excelReader.GroupByServerAndDatabase(filteredRows);

            var result = new GenerationResult { Success = true };
            var allTableDefinitions = new List<TableDefinition>();

            // Step 4: Process each Server/Database combination
            foreach (var serverGroup in groupedData)
            {
                var server = serverGroup.Key;

                foreach (var databaseGroup in serverGroup.Value)
                {
                    var database = databaseGroup.Key;
                    var rows = databaseGroup.Value;

                    ConsoleLogger.LogInfo($"Processing Server: {server}, Database: {database}");

                    // Check if DACPAC exists
                    if (!dacpacExtractor.DacpacExists(inputDirectory, server, database))
                    {
                        ConsoleLogger.LogError($"DACPAC file not found: {server}_{database}.dacpac");
                        result.ErrorsEncountered++;
                        continue;
                    }

                    // Extract model.xml
                    var modelXml = dacpacExtractor.ExtractModelXml(inputDirectory, server, database);
                    if (modelXml == null)
                    {
                        result.ErrorsEncountered++;
                        continue;
                    }

                    // Group by Schema and Table
                    var tableGroups = rows
                        .GroupBy(r => new { r.Schema, r.Table })
                        .ToList();

                    ConsoleLogger.LogInfo($"Found {tableGroups.Count} tables to process");
                    ConsoleLogger.LogInfo("");

                    // Process each table
                    foreach (var tableGroup in tableGroups)
                    {
                        var schema = tableGroup.Key.Schema;
                        var tableName = tableGroup.Key.Table;
                        var requiredColumns = tableGroup.Select(r => r.Column).Distinct().ToList();

                        // Parse table from model.xml
                        var tableDefinition = modelXmlParser.ParseTable(
                            modelXml, server, database, schema, tableName, requiredColumns);

                        if (tableDefinition == null)
                        {
                            result.TablesSkipped++;
                            continue;
                        }

                        // Enrich with primary keys and validate
                        if (!pkEnricher.EnrichTableWithPrimaryKeys(tableDefinition))
                        {
                            result.TablesSkipped++;
                            continue;
                        }

                        // Validate entity class can be generated
                        if (!entityGenerator.ValidateEntityClass(tableDefinition))
                        {
                            result.TablesSkipped++;
                            result.ErrorsEncountered++;
                            continue;
                        }

                        // Generate entity class code
                        var entityCode = entityGenerator.GenerateEntityClass(tableDefinition);

                        // Write to file
                        if (fileWriter.WriteEntityFile(outputDirectory, server, database, schema, tableName, entityCode))
                        {
                            result.EntitiesGenerated++;
                        }
                        else
                        {
                            result.ErrorsEncountered++;
                        }

                        // --- Collect all TableDefinitions for OnModelCreating generation ---
                        allTableDefinitions.Add(tableDefinition);
                    }

                    ConsoleLogger.LogInfo("");
                }
            }

            // --- After all entities are generated, output OnModelCreating body ---
            if (allTableDefinitions.Count > 0)
            {
                var onModelCreatingBody = entityGenerator.GenerateOnModelCreatingBody(allTableDefinitions);
                var dbContextOnModelCreatingPath = Path.Combine(outputDirectory, "DbContext.onModelCreating");
                File.WriteAllText(dbContextOnModelCreatingPath, onModelCreatingBody);
                ConsoleLogger.LogProgress($"Generated OnModelCreating body: ./output/DbContext.onModelCreating");
            }

            // Display summary
            ConsoleLogger.LogInfo("");
            ConsoleLogger.LogInfo("=== Generation Summary ===");
            ConsoleLogger.LogProgress($"Entities generated: {result.EntitiesGenerated}");
            
            if (result.TablesSkipped > 0)
            {
                ConsoleLogger.LogWarning($"Tables skipped: {result.TablesSkipped}");
            }
            
            if (result.ErrorsEncountered > 0)
            {
                ConsoleLogger.LogError($"Errors encountered: {result.ErrorsEncountered}");
            }

            ConsoleLogger.LogInfo("");
            ConsoleLogger.LogProgress("Entity generation completed!");
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Fatal error: {ex.Message}");
            ConsoleLogger.LogError($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
