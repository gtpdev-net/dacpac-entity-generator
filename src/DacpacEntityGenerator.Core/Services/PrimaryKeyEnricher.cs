using DacpacEntityGenerator.Core.Abstractions;
using DacpacEntityGenerator.Core.Models;
using DacpacEntityGenerator.Core.Utilities;

namespace DacpacEntityGenerator.Core.Services;

public class PrimaryKeyEnricher
{
    private readonly IGenerationLogger _logger;

    public PrimaryKeyEnricher(IGenerationLogger logger)
    {
        _logger = logger;
    }

    public bool EnrichTableWithPrimaryKeys(TableDefinition table)
    {
        // Validate that table has at least one column
        if (table.Columns.Count == 0)
        {
            _logger.LogWarning($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Table has no columns after filtering - skipping");
            return false;
        }

        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
        var excelColumns = table.Columns.Where(c => c.IsFromExcel).ToList();

        if (pkColumns.Any())
        {
            // Check if any PK columns were auto-added (not from Excel)
            var autoAddedPkColumns = pkColumns.Where(c => !c.IsFromExcel).ToList();
            
            if (autoAddedPkColumns.Any())
            {
                var columnNames = string.Join(", ", autoAddedPkColumns.Select(c => c.Name));
                _logger.LogInfo($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Auto-added primary key columns: {columnNames}");
            }
        }

        return true;
    }
}
