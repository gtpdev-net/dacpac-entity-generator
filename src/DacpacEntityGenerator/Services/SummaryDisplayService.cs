using Catalogue.Core.DTOs;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

/// <summary>
/// Formats and writes generation and import summaries to the console.
/// Isolating this from orchestration logic makes it straightforward to swap
/// for a UI-bound equivalent during the future Blazor conversion.
/// </summary>
public class SummaryDisplayService
{
    /// <summary>Writes the entity/view generation summary to the console.</summary>
    public void DisplayGenerationSummary(GenerationResult result)
    {
        ConsoleLogger.LogInfo("");
        ConsoleLogger.LogInfo("=== Generation Summary ===");
        ConsoleLogger.LogProgress($"Entities generated: {result.EntitiesGenerated}");
        ConsoleLogger.LogProgress($"Views generated: {result.ViewsGenerated}");

        if (result.TablesSkipped > 0)
            ConsoleLogger.LogWarning($"Tables skipped: {result.TablesSkipped}");

        if (result.ErrorsEncountered > 0)
        {
            ConsoleLogger.LogError($"Errors encountered: {result.ErrorsEncountered}");
            ConsoleLogger.LogInfo("");
            ConsoleLogger.LogInfo("Error details:");
            foreach (var error in result.Errors)
                ConsoleLogger.LogError($"  - {error}");
        }

        ConsoleLogger.LogInfo("");
        ConsoleLogger.LogProgress("Entity generation completed!");
    }

    /// <summary>Writes the Catalogue DB import summary to the console.</summary>
    public void DisplayImportSummary(ImportResultDto importResult, bool dryRun)
    {
        ConsoleLogger.LogInfo("");
        ConsoleLogger.LogInfo("=== Import Summary ===");
        ConsoleLogger.LogProgress($"Tables added:    {importResult.TablesAdded}");
        ConsoleLogger.LogProgress($"Columns added:   {importResult.ColumnsAdded}");
        ConsoleLogger.LogProgress($"Columns updated: {importResult.ColumnsUpdated}");
        ConsoleLogger.LogProgress($"Columns removed: {importResult.ColumnsRemoved}");
        ConsoleLogger.LogProgress($"Columns skipped: {importResult.ColumnsSkipped}");

        if (importResult.Errors.Count > 0)
        {
            ConsoleLogger.LogError($"Import errors: {importResult.Errors.Count}");
            foreach (var err in importResult.Errors)
                ConsoleLogger.LogError($"  - {err}");
        }
        else
        {
            ConsoleLogger.LogProgress(
                dryRun ? "Dry run complete — no changes written."
                       : "Import complete.");
        }
    }
}
