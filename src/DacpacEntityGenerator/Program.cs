using DacpacEntityGenerator.Core.Services;
using DacpacEntityGenerator.Services;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator;

class Program
{
    static void Main(string[] args)
    {
        var logger = new ConsoleGenerationLogger();

        try
        {
            logger.LogInfo("=== DACPAC to Entity Class Generator ===");
            logger.LogInfo("Starting entity generation process...");
            logger.LogInfo("");

            // ── 1. Resolve input/output directories ──────────────────────────
            var pathResolver = new PathResolverService();
            var (inputDirectory, outputDirectory) = pathResolver.ResolveDirectories();

            // ── 2. Run generation pipeline ────────────────────────────────────
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

            // ── 3. Display summary ────────────────────────────────────────────
            var summaryDisplay = new SummaryDisplayService(logger);
            summaryDisplay.DisplayGenerationSummary(result);
        }
        catch (Exception ex)
        {
            logger.LogError($"Fatal error: {ex.Message}");
            logger.LogError($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

