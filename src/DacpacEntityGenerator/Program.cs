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
            var reportWriter = new ReportWriterService();
            var dbContextGenerator = new DbContextGenerator();

            // Ensure output directory exists
            fileWriter.EnsureOutputDirectoryExists(outputDirectory);

            // Collections for global tracking
            var allDiscoveryReports = new List<ElementDiscoveryReport>();
            var allViews = new List<ViewDefinition>();


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
                        var errorMsg = $"DACPAC file not found: {server}_{database}.dacpac";
                        ConsoleLogger.LogError(errorMsg);
                        result.Errors.Add(errorMsg);
                        result.ErrorsEncountered++;
                        continue;
                    }

                    // Extract model.xml
                    var modelXml = dacpacExtractor.ExtractModelXml(inputDirectory, server, database);
                    if (modelXml == null)
                    {
                        var errorMsg = $"Failed to extract model.xml from DACPAC: {server}_{database}.dacpac";
                        result.Errors.Add(errorMsg);
                        result.ErrorsEncountered++;
                        continue;
                    }

                    // Generate discovery report
                    var discoveryReport = modelXmlParser.GenerateDiscoveryReport(modelXml, server, database);
                    allDiscoveryReports.Add(discoveryReport);
                    ConsoleLogger.LogInfo($"[{server}].[{database}] - Discovery: {discoveryReport.StoredProcedures.Count} stored procedures, {discoveryReport.Sequences.Count} sequences, {discoveryReport.Triggers.Count} triggers");

                    // Parse and generate view entities
                    ConsoleLogger.LogInfo($"[{server}].[{database}] - Parsing views from DACPAC");
                    var views = modelXmlParser.ParseViews(modelXml, server, database);
                    
                    foreach (var view in views)
                    {
                        var viewCode = entityGenerator.GenerateViewClass(view);
                        if (fileWriter.WriteViewFile(outputDirectory, server, database, view.Schema, view.ViewName, viewCode))
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
                    {
                        ConsoleLogger.LogProgress($"[{server}].[{database}] - Generated {views.Count} view entities");
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
                            var errorMsg = $"Failed validation for entity class: [{server}].[{database}].[{schema}].[{tableName}]";
                            result.Errors.Add(errorMsg);
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
                            var errorMsg = $"Failed to write entity file: [{server}].[{database}].[{schema}].[{tableName}]";
                            result.Errors.Add(errorMsg);
                            result.ErrorsEncountered++;
                        }

                        // --- Collect all TableDefinitions for OnModelCreating generation ---
                        allTableDefinitions.Add(tableDefinition);
                    }

                    ConsoleLogger.LogInfo("");
                }
            }

            // --- After all entities are generated, generate configuration classes and OnModelCreating body ---
            if (allTableDefinitions.Count > 0 || allViews.Count > 0)
            {
                ConsoleLogger.LogInfo("");
                ConsoleLogger.LogInfo("Generating entity configurations...");

                // Group tables and views by Server/Database
                var tablesByServerDatabase = allTableDefinitions
                    .GroupBy(t => new { t.Server, t.Database })
                    .ToDictionary(g => g.Key, g => g.ToList());

                var viewsByServerDatabase = allViews
                    .GroupBy(v => new { v.Server, v.Database })
                    .ToDictionary(g => g.Key, g => g.ToList());

                var serverDatabasePairs = new List<(string Server, string Database)>();

                // Get all unique server/database combinations
                var allKeys = tablesByServerDatabase.Keys
                    .Union(viewsByServerDatabase.Keys)
                    .Distinct()
                    .ToList();

                // Generate configuration class for each Server/Database combination
                foreach (var key in allKeys)
                {
                    var server = key.Server;
                    var database = key.Database;

                    serverDatabasePairs.Add((server, database));

                    // Get tables and views for this combination
                    tablesByServerDatabase.TryGetValue(key, out var tables);
                    viewsByServerDatabase.TryGetValue(key, out var views);

                    tables ??= new List<TableDefinition>();
                    views ??= new List<ViewDefinition>();

                    // Generate configuration
                    var configBuilder = new System.Text.StringBuilder();
                    
                    if (tables.Count > 0)
                    {
                        var configurationCode = entityGenerator.GenerateEntityConfiguration(server, database, tables);
                        
                        // Extract just the configuration body (without namespace/class wrapper)
                        // We'll reconstruct it with both table and view configurations
                        // Split on all possible line endings to ensure cross-platform compatibility
                        var lines = configurationCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        var inConfigureMethod = false;
                        var skipNextBrace = false;
                        var bodyLines = new List<string>();
                        
                        foreach (var line in lines)
                        {
                            if (line.Contains("public static void Configure(ModelBuilder modelBuilder)"))
                            {
                                inConfigureMethod = true;
                                skipNextBrace = true;
                                continue;
                            }
                            
                            if (inConfigureMethod)
                            {
                                // Skip the opening brace right after method signature
                                if (skipNextBrace && line.Trim() == "{")
                                {
                                    skipNextBrace = false;
                                    continue;
                                }
                                
                                if (line.Trim() == "}" && bodyLines.Count > 0)
                                {
                                    break;
                                }
                                // Don't add the trailing line ending - AppendLine will add it
                                bodyLines.Add(line.TrimEnd());
                            }
                        }
                        
                        // Build complete configuration with both tables and views
                        configBuilder.AppendLine("using Microsoft.EntityFrameworkCore;");
                        configBuilder.AppendLine("using DataLayer.Core.Entities;");
                        configBuilder.AppendLine();
                        
                        var serverPascal = NameConverter.ToPascalCase(server);
                        var databasePascal = NameConverter.ToPascalCase(database);
                        configBuilder.AppendLine($"namespace DataLayer.Core.Configuration.{serverPascal}.{databasePascal}");
                        configBuilder.AppendLine("{");
                        configBuilder.AppendLine($"    public static class {databasePascal}EntityConfiguration");
                        configBuilder.AppendLine("    {");
                        configBuilder.AppendLine("        public static void Configure(ModelBuilder modelBuilder)");
                        configBuilder.AppendLine("        {");
                        
                        // Add table configurations
                        foreach (var line in bodyLines)
                        {
                            configBuilder.AppendLine(line);
                        }
                        
                        // Add view configurations
                        if (views.Count > 0)
                        {
                            configBuilder.AppendLine();
                            configBuilder.AppendLine("            // View Configurations");
                            var viewConfig = entityGenerator.GenerateViewConfiguration(views, server, database);
                            configBuilder.Append(viewConfig);
                        }
                        
                        configBuilder.AppendLine("        }");
                        configBuilder.AppendLine("    }");
                        configBuilder.AppendLine("}");
                        
                        if (fileWriter.WriteConfigurationFile(outputDirectory, server, database, configBuilder.ToString()))
                        {
                            // Configuration file written successfully
                        }
                        else
                        {
                            var errorMsg = $"Failed to write configuration file: [{server}].[{database}]";
                            result.Errors.Add(errorMsg);
                            result.ErrorsEncountered++;
                        }
                    }
                    else if (views.Count > 0)
                    {
                        // Only views, no tables
                        configBuilder.AppendLine("using Microsoft.EntityFrameworkCore;");
                        configBuilder.AppendLine("using DataLayer.Core.Entities;");
                        configBuilder.AppendLine();
                        
                        var serverPascal = NameConverter.ToPascalCase(server);
                        var databasePascal = NameConverter.ToPascalCase(database);
                        configBuilder.AppendLine($"namespace DataLayer.Core.Configuration.{serverPascal}.{databasePascal}");
                        configBuilder.AppendLine("{");
                        configBuilder.AppendLine($"    public static class {databasePascal}EntityConfiguration");
                        configBuilder.AppendLine("    {");
                        configBuilder.AppendLine("        public static void Configure(ModelBuilder modelBuilder)");
                        configBuilder.AppendLine("        {");
                        configBuilder.AppendLine("            // View Configurations");
                        var viewConfig = entityGenerator.GenerateViewConfiguration(views, server, database);
                        configBuilder.Append(viewConfig);
                        configBuilder.AppendLine("        }");
                        configBuilder.AppendLine("    }");
                        configBuilder.AppendLine("}");
                        
                        if (fileWriter.WriteConfigurationFile(outputDirectory, server, database, configBuilder.ToString()))
                        {
                            // Configuration file written successfully
                        }
                        else
                        {
                            var errorMsg = $"Failed to write configuration file: [{server}].[{database}]";
                            result.Errors.Add(errorMsg);
                            result.ErrorsEncountered++;
                        }
                    }
                }

                // Generate simplified OnModelCreating body that calls configuration methods
                var onModelCreatingCalls = entityGenerator.GenerateOnModelCreatingCalls(serverDatabasePairs);
                var dbContextOnModelCreatingPath = Path.Combine(outputDirectory, "DbContext.onModelCreating");
                File.WriteAllText(dbContextOnModelCreatingPath, onModelCreatingCalls);
                ConsoleLogger.LogProgress($"Generated OnModelCreating calls: ./output/DbContext.onModelCreating");

                // Generate complete DacpacDbContext class
                ConsoleLogger.LogInfo("");
                ConsoleLogger.LogInfo("Generating DacpacDbContext...");
                var dbContextCode = dbContextGenerator.GenerateDacpacDbContext(allTableDefinitions, allViews, serverDatabasePairs);
                if (fileWriter.WriteDbContextFile(outputDirectory, dbContextCode))
                {
                    // DbContext generated successfully
                }
                else
                {
                    var errorMsg = "Failed to write DacpacDbContext file";
                    result.Errors.Add(errorMsg);
                    result.ErrorsEncountered++;
                }
            }

            // Write discovery reports
            if (allDiscoveryReports.Any())
            {
                ConsoleLogger.LogInfo("");
                ConsoleLogger.LogInfo("Generating discovery reports...");
                
                if (reportWriter.WriteJsonReport(outputDirectory, allDiscoveryReports))
                {
                    ConsoleLogger.LogProgress($"Generated {allDiscoveryReports.Count} JSON discovery reports");
                }
                
                if (reportWriter.WriteHtmlReport(outputDirectory, allDiscoveryReports))
                {
                    ConsoleLogger.LogProgress($"Generated {allDiscoveryReports.Count} HTML discovery reports");
                }
                
                if (reportWriter.WriteIndexHtml(outputDirectory, allDiscoveryReports))
                {
                    ConsoleLogger.LogProgress("Generated discovery reports index page");
                }
            }

            // Display summary
            ConsoleLogger.LogInfo("");
            ConsoleLogger.LogInfo("=== Generation Summary ===");
            ConsoleLogger.LogProgress($"Entities generated: {result.EntitiesGenerated}");
            ConsoleLogger.LogProgress($"Views generated: {result.ViewsGenerated}");
            
            if (result.TablesSkipped > 0)
            {
                ConsoleLogger.LogWarning($"Tables skipped: {result.TablesSkipped}");
            }
            
            if (result.ErrorsEncountered > 0)
            {
                ConsoleLogger.LogError($"Errors encountered: {result.ErrorsEncountered}");
                ConsoleLogger.LogInfo("");
                ConsoleLogger.LogInfo("Error details:");
                foreach (var error in result.Errors)
                {
                    ConsoleLogger.LogError($"  - {error}");
                }
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
