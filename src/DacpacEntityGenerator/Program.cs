using Catalogue.Infrastructure.Import;
using DacpacEntityGenerator.Services;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator;

class Program
{
    static async Task Main(string[] args)
    {
        // ── 1. Parse CLI options ──────────────────────────────────────────────
        var options = CliOptionsParser.Parse(args);

        try
        {
            ConsoleLogger.LogInfo("=== DACPAC to Entity Class Generator ===");
            ConsoleLogger.LogInfo("Starting entity generation process...");
            ConsoleLogger.LogInfo("");

            // ── 2. Resolve input/output directories ──────────────────────────
            var pathResolver = new PathResolverService();
            var (inputDirectory, outputDirectory) = pathResolver.ResolveDirectories();

            // ── 3. Run generation pipeline ────────────────────────────────────
            var orchestrator = new GenerationOrchestrator(
                new ExcelReaderService(),
                new DacpacExtractorService(),
                new ModelXmlParserService(),
                new PrimaryKeyEnricher(),
                new EntityClassGenerator(),
                new FileWriterService(),
                new ReportWriterService(),
                new DbContextGenerator());

            var result = orchestrator.Run(inputDirectory, outputDirectory);

            // ── 4. Display summary ────────────────────────────────────────────
            var summaryDisplay = new SummaryDisplayService();
            summaryDisplay.DisplayGenerationSummary(result);

            // ── 5. Optional: persist Excel data to Catalogue DB ───────────────
            if (options.ImportToDb)
            {
                ConsoleLogger.LogInfo("");
                ConsoleLogger.LogInfo("=== Catalogue DB Import ===");

                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    ConsoleLogger.LogError(
                        "--import-to-db requires a connection string. " +
                        "Provide --connection-string <value> or set " +
                        "ConnectionStrings:CatalogueDb in appsettings.json.");
                }
                else
                {
                    var excelFilePath = new ExcelReaderService()
                        .FindExcelFile(inputDirectory);

                    if (excelFilePath is null)
                    {
                        ConsoleLogger.LogError("Cannot import to Catalogue DB: no Excel file was found.");
                    }
                    else
                    {
                        var runner       = new CatalogueImportRunner(options.ConnectionString);
                        var importResult = await runner.RunAsync(excelFilePath, options.Strategy, options.DryRun);
                        summaryDisplay.DisplayImportSummary(importResult, options.DryRun);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Fatal error: {ex.Message}");
            ConsoleLogger.LogError($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
