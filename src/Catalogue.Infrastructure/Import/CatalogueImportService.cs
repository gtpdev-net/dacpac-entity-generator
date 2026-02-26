using Catalogue.Core.DTOs;
using Catalogue.Core.Interfaces;
using Catalogue.Core.Models;
using Catalogue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalogue.Infrastructure.Import;

public enum ImportConflictStrategy
{
    SkipExisting,
    AddNewOnly,
    FullSync
}

public class CatalogueImportService
{
    private readonly CatalogueDbContext _db;

    public CatalogueImportService(CatalogueDbContext db)
    {
        _db = db;
    }

    public async Task<ImportResultDto> ImportAsync(
        IReadOnlyList<ImportPreviewRow> preview,
        ImportConflictStrategy strategy,
        bool dryRun)
    {
        var result = new ImportResultDto();

        // Group by server/database/schema/table
        var grouped = preview
            .Where(r => !string.IsNullOrEmpty(r.ColumnName) && string.IsNullOrEmpty(r.Warning))
            .GroupBy(r => new { r.ServerName, r.DatabaseName, r.SchemaName, r.TableName });

        foreach (var grp in grouped)
        {
            // Ensure Source
            var source = await _db.Sources
                .FirstOrDefaultAsync(s => s.ServerName == grp.Key.ServerName);
            if (source is null)
            {
                source = new Source { ServerName = grp.Key.ServerName, IsActive = true };
                if (!dryRun) _db.Sources.Add(source);
                result.TablesAdded++; // count as new
            }

            // Ensure SourceDatabase
            SourceDatabase? database = null;
            if (!dryRun || source.SourceId > 0)
            {
                database = await _db.SourceDatabases
                    .FirstOrDefaultAsync(d => d.SourceId == source.SourceId && d.DatabaseName == grp.Key.DatabaseName);
            }
            if (database is null)
            {
                database = new SourceDatabase
                {
                    Source     = source,
                    DatabaseName = grp.Key.DatabaseName,
                    IsActive     = true
                };
                if (!dryRun) _db.SourceDatabases.Add(database);
            }

            // Ensure SourceTable
            SourceTable? table = null;
            if (!dryRun || (source.SourceId > 0 && database.DatabaseId > 0))
            {
                table = await _db.SourceTables
                    .Include(t => t.Columns)
                    .FirstOrDefaultAsync(t =>
                        t.DatabaseId == database.DatabaseId &&
                        t.SchemaName == grp.Key.SchemaName &&
                        t.TableName  == grp.Key.TableName);
            }
            if (table is null)
            {
                table = new SourceTable
                {
                    Database  = database,
                    SchemaName = grp.Key.SchemaName,
                    TableName  = grp.Key.TableName,
                    IsActive   = true
                };
                if (!dryRun)
                {
                    _db.SourceTables.Add(table);
                    result.TablesAdded++;
                }
            }

            // Apply NumberOfRecords → EstimatedRowCount (take the first non-null value in the group)
            var recordCount = grp.Select(r => r.NumberOfRecords).FirstOrDefault(v => v.HasValue);
            if (!dryRun && recordCount.HasValue)
                table.EstimatedRowCount = recordCount.Value;

            if (!dryRun) await _db.SaveChangesAsync();

            var existingColNames = (table.Columns ?? [])
                .Select(c => c.ColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var incomingColNames = grp.Select(r => r.ColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int sortOrder = table.Columns?.Any() == true
                ? table.Columns.Max(c => c.SortOrder) + 10
                : 10;

            foreach (var row in grp)
            {
                if (existingColNames.Contains(row.ColumnName))
                {
                    if (strategy == ImportConflictStrategy.SkipExisting)
                    {
                        result.ColumnsSkipped++;
                        continue;
                    }
                    // Update intent flags for AddNewOnly and FullSync
                    if (!dryRun && strategy != ImportConflictStrategy.SkipExisting)
                    {
                        var existing = table.Columns!.First(c =>
                            c.ColumnName.Equals(row.ColumnName, StringComparison.OrdinalIgnoreCase));
                        existing.IsInDaoAnalysis  = row.IsInDaoAnalysis;
                        existing.IsAddedByApi     = row.IsAddedByApi;
                        existing.IsSelectedForLoad= row.IsSelectedForLoad;
                        existing.PersistenceType  = row.PersistenceType;
                        result.ColumnsUpdated++;
                    }
                }
                else
                {
                    if (!dryRun)
                    {
                        _db.SourceColumns.Add(new SourceColumn
                        {
                            TableId           = table.TableId,
                            ColumnName        = row.ColumnName,
                            PersistenceType   = row.PersistenceType,
                            IsInDaoAnalysis   = row.IsInDaoAnalysis,
                            IsAddedByApi      = row.IsAddedByApi,
                            IsSelectedForLoad = row.IsSelectedForLoad,
                            SortOrder         = sortOrder,
                            IsActive          = true
                        });
                        sortOrder += 10;
                    }
                    result.ColumnsAdded++;
                }
            }

            // FullSync: remove columns no longer present
            if (strategy == ImportConflictStrategy.FullSync && !dryRun && table.Columns is not null)
            {
                var toRemove = table.Columns
                    .Where(c => !incomingColNames.Contains(c.ColumnName))
                    .ToList();
                _db.SourceColumns.RemoveRange(toRemove);
                result.ColumnsRemoved += toRemove.Count;
            }

            if (!dryRun)
                await _db.SaveChangesAsync();
        }

        return result;
    }
}
