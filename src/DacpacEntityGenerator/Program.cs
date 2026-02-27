using Catalogue.Core.Abstractions;
using Dacpac.Management.Services;
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

            // Derived output paths (maintain backward-compatible SQL layout;
            // SQLite output goes in a sibling SQLite/ subdirectory)
            var sqlEntityAndConfigOutputDir = outputDirectory;
            var sqlDbContextFilePath        = Path.Combine(outputDirectory, "SQLDbContext.cs");
            var sqliteConfigOutputDir       = Path.Combine(outputDirectory, "SQLite");
            var sqliteDbContextFilePath     = Path.Combine(outputDirectory, "SQLite", "SQLiteDbContext.cs");

            // ── 2. Run generation pipeline ────────────────────────────────────
            var orchestrator = new GenerationOrchestrator(
                new ExcelReaderService(logger),
                new DacpacExtractorService(logger),
                new ModelXmlParserService(logger),
                new PrimaryKeyEnricher(logger),
                new EntityClassGenerator(logger),
                new EntityConfigurationGenerator(logger),
                new FileWriterService(logger),
                new DbContextGenerator(),
                logger);

            var result = orchestrator.Run(
                inputDirectory,
                sqlEntityAndConfigOutputDir,
                sqlDbContextFilePath,
                sqliteConfigOutputDir,
                sqliteDbContextFilePath);

            // ── 3. Display summary ────────────────────────────────────────────
            var summaryDisplay = new SummaryDisplayService(logger);
            summaryDisplay.DisplayGenerationSummary(result);
        }
        catch (Exception ex)
        {
            var logger2 = new ConsoleGenerationLogger();
            logger2.LogError($"Fatal error: {ex.Message}");
            logger2.LogError($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}

