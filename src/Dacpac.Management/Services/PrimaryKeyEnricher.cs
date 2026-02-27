using Catalogue.Core.Abstractions;
using Dacpac.Management.Models;

namespace Dacpac.Management.Services;

public class PrimaryKeyEnricher
{
    private readonly IGenerationLogger _logger;

    public PrimaryKeyEnricher(IGenerationLogger logger)
    {
        _logger = logger;
    }

    public bool EnrichTableWithPrimaryKeys(TableDefinition table)
    {
        if (table.Columns.Count == 0)
        {
            _logger.LogWarning($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Table has no columns after filtering - skipping");
            return false;
        }

        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();

        if (pkColumns.Any())
        {
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
