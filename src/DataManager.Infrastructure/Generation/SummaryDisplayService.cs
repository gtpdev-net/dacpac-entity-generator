using DataManager.Core.Abstractions;
using DataManager.Core.Models.Dacpac;

namespace DataManager.Infrastructure.Generation;

/// <summary>
/// Formats and writes generation and import summaries to the console.
/// Isolating this from orchestration logic makes it straightforward to swap
/// for a UI-bound equivalent during the future Blazor conversion.
/// </summary>
public class SummaryDisplayService
{
    private readonly IGenerationLogger _logger;

    public SummaryDisplayService(IGenerationLogger logger)
    {
        _logger = logger;
    }

    /// <summary>Writes the entity/view generation summary to the console.</summary>
    public void DisplayGenerationSummary(GenerationResult result)
    {
        _logger.LogInfo("");
        _logger.LogInfo("=== Generation Summary ===");
        _logger.LogProgress($"Entities generated: {result.EntitiesGenerated}");
        _logger.LogProgress($"Views generated: {result.ViewsGenerated}");

        if (result.TablesSkipped > 0)
            _logger.LogWarning($"Tables skipped: {result.TablesSkipped}");

        if (result.ErrorsEncountered > 0)
        {
            _logger.LogError($"Errors encountered: {result.ErrorsEncountered}");
            _logger.LogInfo("");
            _logger.LogInfo("Error details:");
            foreach (var error in result.Errors)
                _logger.LogError($"  - {error}");
        }

        _logger.LogInfo("");
        _logger.LogProgress("Entity generation completed!");
    }
}
