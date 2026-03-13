using DataManager.Core.DTOs;
using DataManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataManager.Infrastructure.Import;

public class DataManagerImportService
{
    private readonly DataManagerDbContext _db;

    public DataManagerImportService(DataManagerDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Updates catalog flags (<c>PersistenceType</c>, <c>IsInDaoAnalysis</c>, <c>IsAddedByApi</c>,
    /// <c>IsSelectedForLoad</c>) on existing <see cref="Core.Models.Entities.SourceColumn"/> records
    /// identified by the server/database/schema/table/column keys in <paramref name="preview"/>.
    /// Also updates <c>SourceTable.EstimatedRowCount</c> when a <c>NumberOfRecords</c> value is present.
    /// <para>
    /// Excel import does <strong>not</strong> create, modify, or delete any other data.
    /// All schema records must already exist from a prior DACPAC import.
    /// Rows that cannot be matched to an existing active column are counted in
    /// <see cref="ImportResultDto.ColumnsNotFound"/> and reported as warnings.
    /// </para>
    /// </summary>
    public async Task<ImportResultDto> ImportAsync(
        IReadOnlyList<ImportPreviewRow> preview,
        bool dryRun)
    {
        var result = new ImportResultDto();

        // Group by server/database/schema/table for efficient per-table lookup.
        var grouped = preview
            .Where(r => !string.IsNullOrEmpty(r.ColumnName) && string.IsNullOrEmpty(r.Warning))
            .GroupBy(r => new { r.ServerName, r.DatabaseName, r.SchemaName, r.TableName });

        foreach (var grp in grouped)
        {
            // Look up the SourceTable — must already exist from a DACPAC import.
            // Excel import does not create schema records.
            var table = await _db.SourceTables
                .Include(t => t.Columns.Where(c => c.IsActive))
                .Include(t => t.Database).ThenInclude(d => d.Server)
                .FirstOrDefaultAsync(t =>
                    t.IsActive &&
                    t.Database.Server.ServerName == grp.Key.ServerName &&
                    t.Database.DatabaseName      == grp.Key.DatabaseName &&
                    t.SchemaName                 == grp.Key.SchemaName &&
                    t.TableName                  == grp.Key.TableName);

            if (table is null)
            {
                var label = $"[{grp.Key.ServerName}].[{grp.Key.DatabaseName}].[{grp.Key.SchemaName}].[{grp.Key.TableName}]";
                result.TablesNotFound++;
                result.ColumnsNotFound += grp.Count();
                result.Warnings.Add($"{label} not found in schema — import DACPAC first.");
                continue;
            }

            // EstimatedRowCount is the one table-level field Excel owns.
            var recordCount = grp.Select(r => r.NumberOfRecords).FirstOrDefault(v => v.HasValue);
            if (!dryRun && recordCount.HasValue)
                table.EstimatedRowCount = recordCount.Value;

            // Update catalog flags on each matched column — all other fields are untouched.
            foreach (var row in grp)
            {
                var col = table.Columns?
                    .FirstOrDefault(c => c.ColumnName.Equals(row.ColumnName, StringComparison.OrdinalIgnoreCase));

                if (col is null)
                {
                    result.ColumnsNotFound++;
                    result.Warnings.Add(
                        $"[{grp.Key.ServerName}].[{grp.Key.DatabaseName}].[{grp.Key.SchemaName}].[{grp.Key.TableName}].[{row.ColumnName}] not found in schema — import DACPAC first.");
                    continue;
                }

                if (!dryRun)
                {
                    col.PersistenceType   = row.PersistenceType;
                    col.IsInDaoAnalysis   = row.IsInDaoAnalysis;
                    col.IsAddedByApi      = row.IsAddedByApi;
                    col.IsSelectedForLoad = row.IsSelectedForLoad;
                }

                result.ColumnsUpdated++;
            }

            if (!dryRun)
                await _db.SaveChangesAsync();
        }

        return result;
    }
}
