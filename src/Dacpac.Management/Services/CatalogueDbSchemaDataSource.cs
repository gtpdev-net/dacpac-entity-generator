using Catalogue.Core.DTOs;
using Catalogue.Core.Interfaces;
using Catalogue.Core.Models;
using Dacpac.Management.Abstractions;
using Dacpac.Management.Models;

namespace Dacpac.Management.Services;

/// <summary>
/// An <see cref="ISchemaDataSource"/> that reads schema from CatalogueDb via
/// <see cref="ICatalogueRepository"/>.  Set <see cref="DatabaseId"/> before
/// calling any methods.
/// </summary>
public class CatalogueDbSchemaDataSource : ISchemaDataSource
{
    private readonly ICatalogueRepository _repo;

    /// <summary>
    /// The CatalogueDb database to generate code for.
    /// Must be set before calling any interface methods.
    /// </summary>
    public int DatabaseId { get; set; }

    public CatalogueDbSchemaDataSource(ICatalogueRepository repo)
    {
        _repo = repo;
    }

    /// <inheritdoc />
    public async Task<List<TableDefinition>> GetTablesForGenerationAsync()
    {
        if (DatabaseId == 0)
            return new List<TableDefinition>();

        if (DatabaseId < 0)
        {
            var allDbs  = await _repo.GetInScopeDatabasesAsync();
            var results = new List<TableDefinition>();
            foreach (var dbInfo in allDbs)
                results.AddRange(await GetTablesForDatabaseAsync(dbInfo.DatabaseId));
            return results;
        }

        return await GetTablesForDatabaseAsync(DatabaseId);
    }

    private async Task<List<TableDefinition>> GetTablesForDatabaseAsync(int databaseId)
    {
        var db = await _repo.GetDatabaseByIdAsync(databaseId);
        if (db == null)
            return new List<TableDefinition>();

        var serverName   = db.Source?.ServerName ?? string.Empty;
        var databaseName = db.DatabaseName;

        var tableInfoList = await _repo.GetInScopeTablesAsync(databaseId);
        var result        = new List<TableDefinition>();

        foreach (var tableInfo in tableInfoList)
        {
            var table = await _repo.GetTableByIdAsync(tableInfo.TableId);
            if (table == null) continue;

            // Columns eligible for relational EF generation:
            //   IsActive=true, IsSelectedForLoad=true, PersistenceType ≠ 'D'
            var eligibleColumns = table.Columns
                .Where(c => c.IsActive && c.IsSelectedForLoad && c.PersistenceType != 'D')
                .OrderBy(c => c.SortOrder)
                .ToList();

            if (eligibleColumns.Count == 0) continue;

            var tableDefinition = new TableDefinition
            {
                Server   = serverName,
                Database = databaseName,
                Schema   = table.SchemaName,
                TableName = table.TableName,
                Columns  = eligibleColumns.Select(MapColumn).ToList(),
            };

            result.Add(tableDefinition);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<List<ViewDefinition>> GetViewsAsync()
    {
        if (DatabaseId == 0)
            return new List<ViewDefinition>();

        if (DatabaseId < 0)
        {
            var allDbs  = await _repo.GetInScopeDatabasesAsync();
            var results = new List<ViewDefinition>();
            foreach (var dbInfo in allDbs)
                results.AddRange(await GetViewsForDatabaseAsync(dbInfo.DatabaseId));
            return results;
        }

        return await GetViewsForDatabaseAsync(DatabaseId);
    }

    private async Task<List<ViewDefinition>> GetViewsForDatabaseAsync(int databaseId)
    {
        var db = await _repo.GetDatabaseByIdAsync(databaseId);
        if (db == null)
            return new List<ViewDefinition>();

        var serverName   = db.Source?.ServerName ?? string.Empty;
        var databaseName = db.DatabaseName;

        var viewSummaries = await _repo.GetViewsAsync(databaseId);
        var result        = new List<ViewDefinition>();

        foreach (var summary in viewSummaries)
        {
            if (!summary.IsActive) continue;

            var detail = await _repo.GetViewByIdAsync(summary.SourceViewId);
            if (detail == null) continue;

            var viewDef = new ViewDefinition
            {
                Server   = serverName,
                Database = databaseName,
                Schema   = detail.SchemaName,
                ViewName = detail.ViewName,
                SqlBody  = detail.SqlBody,
                HasStandardAuditColumns = detail.HasStandardAuditColumns,
                Columns  = detail.Columns
                    .Select(c => new ColumnDefinition
                    {
                        Name      = c.ColumnName,
                        SqlType   = c.SqlType ?? string.Empty,
                        IsNullable = c.IsNullable,
                        MaxLength  = c.MaxLength,
                        Precision  = c.Precision,
                        Scale      = c.Scale,
                    })
                    .ToList(),
            };

            result.Add(viewDef);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ElementDiscoveryReport> GetDiscoveryReportAsync()
    {
        if (DatabaseId == 0)
            return new ElementDiscoveryReport();

        if (DatabaseId < 0)
        {
            var allDbs   = await _repo.GetInScopeDatabasesAsync();
            var allSps   = new List<ElementDetail>();
            var allTrigs = new List<ElementDetail>();
            var totalSps   = 0;
            var totalFns   = 0;
            var totalTrigs = 0;
            foreach (var dbInfo in allDbs)
            {
                var dbReport = await GetDiscoveryForDatabaseAsync(dbInfo.DatabaseId);
                allSps.AddRange(dbReport.StoredProcedures);
                allTrigs.AddRange(dbReport.Triggers);
                totalSps   += dbReport.ElementTypeCounts.GetValueOrDefault("StoredProcedure");
                totalFns   += dbReport.ElementTypeCounts.GetValueOrDefault("Function");
                totalTrigs += dbReport.ElementTypeCounts.GetValueOrDefault("Trigger");
            }
            return new ElementDiscoveryReport
            {
                StoredProcedures  = allSps,
                Triggers          = allTrigs,
                ElementTypeCounts = new Dictionary<string, int>
                {
                    ["StoredProcedure"] = totalSps,
                    ["Function"]        = totalFns,
                    ["Trigger"]         = totalTrigs,
                },
            };
        }

        return await GetDiscoveryForDatabaseAsync(DatabaseId);
    }

    private async Task<ElementDiscoveryReport> GetDiscoveryForDatabaseAsync(int databaseId)
    {
        var db = await _repo.GetDatabaseByIdAsync(databaseId);
        if (db == null)
            return new ElementDiscoveryReport();

        var serverName   = db.Source?.ServerName ?? string.Empty;
        var databaseName = db.DatabaseName;
        var location     = $"[{serverName}].[{databaseName}]";

        // Stored procedures
        var storedProcs = await _repo.GetStoredProceduresAsync(databaseId);
        var spDetails = storedProcs
            .Select(p => new ElementDetail
            {
                Name     = $"{p.SchemaName}.{p.ProcedureName}",
                Location = location,
                Type     = "StoredProcedure",
                Details  = p.HasSqlBody ? "Has SQL body" : string.Empty,
            })
            .ToList();

        // Functions
        var functions = await _repo.GetFunctionsAsync(databaseId);
        // (functions are returned as ElementTypeCounts entry, not a dedicated list on the report)

        // Triggers (gathered from all tables)
        var tableInfoList = await _repo.GetInScopeTablesAsync(databaseId);
        var triggerDetails = new List<ElementDetail>();
        foreach (var tableInfo in tableInfoList)
        {
            var triggers = await _repo.GetTriggersAsync(tableInfo.TableId);
            triggerDetails.AddRange(triggers.Select(t => new ElementDetail
            {
                Name     = $"{t.SchemaName}.{t.TriggerName}",
                Location = $"{location}.[{tableInfo.SchemaName}].[{tableInfo.TableName}]",
                Type     = "Trigger",
                Details  = t.HasSqlBody ? "Has SQL body" : string.Empty,
            }));
        }

        var typeCounts = new Dictionary<string, int>
        {
            ["StoredProcedure"] = storedProcs.Count,
            ["Function"]        = functions.Count,
            ["Trigger"]         = triggerDetails.Count,
        };

        return new ElementDiscoveryReport
        {
            Server           = serverName,
            Database         = databaseName,
            StoredProcedures = spDetails,
            Triggers         = triggerDetails,
            ElementTypeCounts = typeCounts,
        };
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static ColumnDefinition MapColumn(SourceColumn col) => new()
    {
        Name               = col.ColumnName,
        SqlType            = col.SqlType ?? string.Empty,
        IsNullable         = col.IsNullable,
        MaxLength          = col.MaxLength,
        IsIdentity         = col.IsIdentity,
        IsPrimaryKey       = col.IsPrimaryKey,
        IsFromExcel        = false,
        Precision          = col.Precision,
        Scale              = col.Scale,
        DefaultValue       = col.DefaultValue,
        IsComputed         = col.IsComputed,
        IsComputedPersisted = col.IsComputedPersisted,
        ComputedExpression = col.ComputedExpression,
        IsRowVersion       = col.IsRowVersion,
        IsConcurrencyToken = col.IsConcurrencyToken,
        Collation          = col.Collation,
        Description        = col.Description,
    };
}
