using DataManager.Core.DTOs;
using DataManager.Core.Models.Entities;
using DataManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataManager.Infrastructure.Import;

public class MigrationConfigLoadService
{
    private readonly IDbContextFactory<DataManagerDbContext> _factory;

    public MigrationConfigLoadService(IDbContextFactory<DataManagerDbContext> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Builds MigrationConfig rows for all SourceTables that have at least one column
    /// where IsSelectedForLoad = true AND PersistenceType IN ('R', 'B').
    /// Upserts: auto-derived fields are always refreshed; user-edited fields are preserved
    /// (DestinationServer, DestinationDatabase, FilterCondition).
    /// Tables that no longer qualify are deactivated (IsActive = false) so that
    /// ADF incremental queries (WHERE ModifiedAt >= @date) detect the removal.
    /// </summary>
    public async Task<MigrationConfigLoadResult> LoadMigrationConfigsAsync()
    {
        using var db = _factory.CreateDbContext();

        var result = new MigrationConfigLoadResult();

        // Fetch all qualifying tables with their qualifying columns
        var qualifyingTables = await db.SourceTables
            .Include(t => t.Database)
                .ThenInclude(d => d.Server)
            .Include(t => t.Columns)
            .Where(t => t.IsActive &&
                        t.Columns.Any(c => c.IsActive &&
                                          c.IsSelectedForLoad &&
                                          (c.PersistenceType == 'R' || c.PersistenceType == 'B')))
            .ToListAsync();

        result.TotalTablesEvaluated = qualifyingTables.Count;

        // Load all existing MigrationConfig rows indexed by TableId
        var existingMap = await db.MigrationConfigs
            .ToDictionaryAsync(m => m.TableId);

        var qualifyingTableIds = new HashSet<int>(qualifyingTables.Select(t => t.TableId));
        var now = DateTime.UtcNow;

        foreach (var table in qualifyingTables)
        {
            var qualifyingColumns = table.Columns
                .Where(c => c.IsActive &&
                            c.IsSelectedForLoad &&
                            (c.PersistenceType == 'R' || c.PersistenceType == 'B'))
                .OrderBy(c => c.SortOrder)
                .Select(c => c.ColumnName)
                .ToList();

            var columnList  = string.Join(",", qualifyingColumns);
            var srcServer   = table.Database.Server.ServerName;
            var srcDatabase = table.Database.DatabaseName;
            var srcSchema   = table.SchemaName;
            var srcTable    = table.TableName;
            var destSchema  = srcDatabase;  // destination schema = source database name
            var destTable   = srcTable;     // destination table  = source table name

            if (existingMap.TryGetValue(table.TableId, out var existing))
            {
                // Re-activation counts as a change: always refresh computed fields and
                // bump ModifiedAt so ADF incremental queries see the row again.
                var changed = !existing.IsActive                    ||
                              existing.SourceServer      != srcServer   ||
                              existing.SourceDatabase    != srcDatabase ||
                              existing.SourceSchema      != srcSchema   ||
                              existing.SourceTableName   != srcTable    ||
                              existing.DestinationSchema != destSchema  ||
                              existing.DestinationTable  != destTable   ||
                              existing.ColumnList        != columnList;

                if (changed)
                {
                    existing.IsActive          = true;
                    existing.SourceServer      = srcServer;
                    existing.SourceDatabase    = srcDatabase;
                    existing.SourceSchema      = srcSchema;
                    existing.SourceTableName   = srcTable;
                    existing.DestinationSchema = destSchema;
                    existing.DestinationTable  = destTable;
                    existing.ColumnList        = columnList;
                    existing.ModifiedAt        = now;
                    existing.ModifiedBy        = "system";
                    result.Updated++;
                }
                else
                {
                    result.Unchanged++;
                }
            }
            else
            {
                db.MigrationConfigs.Add(new MigrationConfig
                {
                    TableId             = table.TableId,
                    SourceServer        = srcServer,
                    SourceDatabase      = srcDatabase,
                    SourceSchema        = srcSchema,
                    SourceTableName     = srcTable,
                    DestinationServer   = null,
                    DestinationDatabase = null,
                    DestinationSchema   = destSchema,
                    DestinationTable    = destTable,
                    ColumnList          = columnList,
                    FilterCondition     = null,
                    IsActive            = true,
                    CreatedAt           = now,
                    CreatedBy           = "system"
                });
                result.Inserted++;
            }
        }

        // Deactivate configs whose table no longer qualifies.
        // Setting ModifiedAt ensures ADF incremental queries detect the removal.
        foreach (var (tableId, stale) in existingMap)
        {
            if (!qualifyingTableIds.Contains(tableId) && stale.IsActive)
            {
                stale.IsActive   = false;
                stale.ModifiedAt = now;
                stale.ModifiedBy = "system";
                result.Deactivated++;
            }
        }

        await db.SaveChangesAsync();
        return result;
    }

    /// <summary>
    /// Refreshes (or creates / deactivates) the MigrationConfig for a single table.
    /// Called automatically whenever a column's IsSelectedForLoad or PersistenceType
    /// changes on the catalogue page, so the ADF pipeline's incremental watermark
    /// (WHERE ModifiedAt >= @date) immediately reflects the change.
    /// </summary>
    public async Task RefreshMigrationConfigForTableAsync(int tableId)
    {
        using var db = _factory.CreateDbContext();

        var table = await db.SourceTables
            .Include(t => t.Database)
                .ThenInclude(d => d.Server)
            .Include(t => t.Columns)
            .FirstOrDefaultAsync(t => t.TableId == tableId);

        if (table is null) return;

        var qualifyingColumns = table.Columns
            .Where(c => c.IsActive &&
                        c.IsSelectedForLoad &&
                        (c.PersistenceType == 'R' || c.PersistenceType == 'B'))
            .OrderBy(c => c.SortOrder)
            .Select(c => c.ColumnName)
            .ToList();

        var existing = await db.MigrationConfigs
            .FirstOrDefaultAsync(m => m.TableId == tableId);

        var now = DateTime.UtcNow;

        if (qualifyingColumns.Count > 0)
        {
            var columnList  = string.Join(",", qualifyingColumns);
            var srcServer   = table.Database.Server.ServerName;
            var srcDatabase = table.Database.DatabaseName;
            var srcSchema   = table.SchemaName;
            var srcTable    = table.TableName;
            var destSchema  = srcDatabase;
            var destTable   = srcTable;

            if (existing is not null)
            {
                var changed = !existing.IsActive                    ||
                              existing.SourceServer      != srcServer   ||
                              existing.SourceDatabase    != srcDatabase ||
                              existing.SourceSchema      != srcSchema   ||
                              existing.SourceTableName   != srcTable    ||
                              existing.DestinationSchema != destSchema  ||
                              existing.DestinationTable  != destTable   ||
                              existing.ColumnList        != columnList;

                if (changed)
                {
                    existing.IsActive          = true;
                    existing.SourceServer      = srcServer;
                    existing.SourceDatabase    = srcDatabase;
                    existing.SourceSchema      = srcSchema;
                    existing.SourceTableName   = srcTable;
                    existing.DestinationSchema = destSchema;
                    existing.DestinationTable  = destTable;
                    existing.ColumnList        = columnList;
                    existing.ModifiedAt        = now;
                    existing.ModifiedBy        = "system";
                }
            }
            else
            {
                db.MigrationConfigs.Add(new MigrationConfig
                {
                    TableId             = tableId,
                    SourceServer        = srcServer,
                    SourceDatabase      = srcDatabase,
                    SourceSchema        = srcSchema,
                    SourceTableName     = srcTable,
                    DestinationServer   = null,
                    DestinationDatabase = null,
                    DestinationSchema   = destSchema,
                    DestinationTable    = destTable,
                    ColumnList          = columnList,
                    FilterCondition     = null,
                    IsActive            = true,
                    CreatedAt           = now,
                    CreatedBy           = "system"
                });
            }
        }
        else if (existing is not null && existing.IsActive)
        {
            // No qualifying columns remain — deactivate so ADF picks up the change.
            existing.IsActive   = false;
            existing.ModifiedAt = now;
            existing.ModifiedBy = "system";
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Populates MigrationConfig rows on application startup if the table is empty.
    /// Returns null if the table already had data (no action taken).
    /// </summary>
    public async Task<MigrationConfigLoadResult?> EnsurePopulatedAsync()
    {
        using var db = _factory.CreateDbContext();
        if (await db.MigrationConfigs.AnyAsync())
            return null;

        return await LoadMigrationConfigsAsync();
    }
}
