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
    /// (DestinationServer, DestinationDatabase, FilterCondition, IsActive).
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
                // Check if anything actually changed before touching ModifiedAt
                var changed = existing.SourceServer      != srcServer   ||
                              existing.SourceDatabase    != srcDatabase ||
                              existing.SourceSchema      != srcSchema   ||
                              existing.SourceTableName   != srcTable    ||
                              existing.DestinationSchema != destSchema  ||
                              existing.DestinationTable  != destTable   ||
                              existing.ColumnList        != columnList;

                if (changed)
                {
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
                    TableId           = table.TableId,
                    SourceServer      = srcServer,
                    SourceDatabase    = srcDatabase,
                    SourceSchema      = srcSchema,
                    SourceTableName   = srcTable,
                    DestinationServer   = null,
                    DestinationDatabase = null,
                    DestinationSchema = destSchema,
                    DestinationTable  = destTable,
                    ColumnList        = columnList,
                    FilterCondition   = null,
                    IsActive          = true,
                    CreatedAt         = now,
                    CreatedBy         = "system"
                });
                result.Inserted++;
            }
        }

        await db.SaveChangesAsync();
        return result;
    }
}
