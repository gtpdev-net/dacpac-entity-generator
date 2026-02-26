using DacpacEntityGenerator.Core.Services;
using DacpacEntityGenerator.Services;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator;

class Program
{
    static async Task Main(string[] args)
    {
        // ── 1. Parse CLI options ──────────────────────────────────────────────
        var options = CliOptionsParser.Parse(args);

        var logger = new ConsoleGenerationLogger();

        try
        {
            logger.LogInfo("=== DACPAC to Entity Class Generator ===");
            logger.LogInfo("Starting entity generation process...");
            logger.LogInfo("");

            // ── 2. Resolve input/output directories ──────────────────────────
            var pathResolver = new PathResolverService();
            var (inputDirectory, outputDirectory) = pathResolver.ResolveDirectories();

            // ── 3. Run generation pipeline ────────────────────────────────────
            var orchestrator = new GenerationOrchestrator(
                new ExcelReaderService(logger),
                new DacpacExtractorService(logger),
                new ModelXmlParserService(logger),
                new PrimaryKeyEnricher(logger),
                new EntityClassGenerator(logger),
                new FileWriterService(logger),
                new DbContextGenerator(),
                logger);

            var result = orchestrator.Run(inputDirectory, outputDirectory);

            // ── 4. Display summary ────────────────────────────────────────────
            var summaryDisplay = new SummaryDisplayService(logger);
            summaryDisplay.DisplayGenerationSummary(result);

            // ── 5. Optional: persist Excel data to Catalogue DB ───────────────
            if (options.ImportToDb)
            {
                logger.LogInfo("");
                logger.LogInfo("=== Catalogue DB Import ===");

                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    logger.LogError(
                        "--import-to-db requires a connection string. " +
                        "Provide --connection-string <value> or set " +
                        "ConnectionStrings:CatalogueDb in appsettings.json.");
                }
                else
                {
                    var excelFilePath = new ExcelReaderService(logger)
                        .FindExcelFile(inputDirectory);

                    if (excelFilePath is null)
                    {
                        logger.LogError("Cannot import to Catalogue DB: no Excel file was found.");
                    }
                    else
                    {
                        var runner       = new CatalogueImportRunner(options.ConnectionString);
                        var importResult = await runner.RunAsync(excelFilePath, options.Strategy, options.DryRun);

                        logger.LogInfo("");
                        logger.LogInfo("=== Import Summary ===");
                        logger.LogProgress($"Tables added:    {importResult.TablesAdded}");
                        logger.LogProgress($"Columns added:   {importResult.ColumnsAdded}");
                        logger.LogProgress($"Columns updated: {importResult.ColumnsUpdated}");
                        logger.LogProgress($"Columns removed: {importResult.ColumnsRemoved}");
                        logger.LogProgress($"Columns skipped: {importResult.ColumnsSkipped}");

                        if (importResult.Errors.Count > 0)
                        {
                            logger.LogError($"Import errors: {importResult.Errors.Count}");
                            foreach (var err in importResult.Errors)
                                logger.LogError($"  - {err}");
                        }
                        else
                        {
                            logger.LogProgress(options.DryRun
                                ? "Dry run complete — no changes written."
                                : "Import complete.");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Fatal error: {ex.Message}");
            logger.LogError($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

