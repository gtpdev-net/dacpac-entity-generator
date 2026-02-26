using Catalogue.Core.DTOs;
using Catalogue.Core.Interfaces;
using Catalogue.Core.Models;
using Catalogue.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Catalogue.Infrastructure.Repositories;

public class EfCatalogueRepository : ICatalogueRepository
{
    private readonly CatalogueDbContext _db;

    public EfCatalogueRepository(CatalogueDbContext db)
    {
        _db = db;
    }

    // ── Sources ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Source>> GetSourcesAsync(bool includeInactive = false)
    {
        var q = _db.Sources.AsQueryable();
        if (!includeInactive) q = q.Where(x => x.IsActive);
        return await q.OrderBy(x => x.ServerName).ToListAsync();
    }

    public Task<Source?> GetSourceByIdAsync(int sourceId)
        => _db.Sources.FirstOrDefaultAsync(x => x.SourceId == sourceId);

    public async Task<Source> AddSourceAsync(Source source)
    {
        _db.Sources.Add(source);
        await _db.SaveChangesAsync();
        return source;
    }

    public async Task UpdateSourceAsync(Source source)
    {
        _db.Sources.Update(source);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSourceAsync(int sourceId)
    {
        var entity = await _db.Sources.FindAsync(sourceId);
        if (entity is null) return;
        _db.Sources.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Databases ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceDatabaseInfo>> GetInScopeDatabasesAsync(
        int? sourceId = null, bool includeInactive = false)
    {
        var q = _db.SourceDatabases
            .Include(d => d.Source)
            .Include(d => d.Tables)
                .ThenInclude(t => t.Columns)
            .AsQueryable();

        if (!includeInactive) q = q.Where(x => x.IsActive);
        if (sourceId.HasValue) q = q.Where(x => x.SourceId == sourceId.Value);

        return await q.Select(d => new SourceDatabaseInfo
        {
            DatabaseId = d.DatabaseId,
            SourceId = d.SourceId,
            ServerName = d.Source.ServerName,
            DatabaseName = d.DatabaseName,
            Description = d.Description,
            IsActive = d.IsActive,
            TableCount = d.Tables.Count(t => t.IsActive),
            InScopeColumnCount = d.Tables
                .Where(t => t.IsActive)
                .SelectMany(t => t.Columns)
                .Count(c => c.IsActive && (c.IsInDaoAnalysis || c.IsAddedByApi)),
            SelectedForLoadCount = d.Tables
                .Where(t => t.IsActive)
                .SelectMany(t => t.Columns)
                .Count(c => c.IsActive && c.IsSelectedForLoad)
        }).OrderBy(d => d.ServerName).ThenBy(d => d.DatabaseName).ToListAsync();
    }

    public Task<SourceDatabase?> GetDatabaseByIdAsync(int databaseId)
        => _db.SourceDatabases.Include(d => d.Source).FirstOrDefaultAsync(x => x.DatabaseId == databaseId);

    public async Task<SourceDatabase> AddDatabaseAsync(SourceDatabase database)
    {
        _db.SourceDatabases.Add(database);
        await _db.SaveChangesAsync();
        return database;
    }

    public async Task UpdateDatabaseAsync(SourceDatabase database)
    {
        _db.SourceDatabases.Update(database);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteDatabaseAsync(int databaseId)
    {
        var entity = await _db.SourceDatabases.FindAsync(databaseId);
        if (entity is null) return;
        _db.SourceDatabases.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Tables ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceTableInfo>> GetInScopeTablesAsync(
        int? databaseId = null, bool includeInactive = false)
    {
        var q = _db.SourceTables
            .Include(t => t.Database)
                .ThenInclude(d => d.Source)
            .Include(t => t.Columns)
            .AsQueryable();

        if (!includeInactive) q = q.Where(x => x.IsActive);
        if (databaseId.HasValue) q = q.Where(x => x.DatabaseId == databaseId.Value);

        return await q.Select(t => new SourceTableInfo
        {
            TableId = t.TableId,
            DatabaseId = t.DatabaseId,
            DatabaseName = t.Database.DatabaseName,
            SchemaName = t.SchemaName,
            TableName = t.TableName,
            EstimatedRowCount = t.EstimatedRowCount,
            Notes = t.Notes,
            IsActive = t.IsActive,
            TotalColumnCount = t.Columns.Count(c => c.IsActive),
            InScopeRelationalCount = t.Columns.Count(c => c.IsActive
                && (c.IsInDaoAnalysis || c.IsAddedByApi) && (c.PersistenceType == 'R' || c.PersistenceType == 'B')),
            InScopeDocumentCount = t.Columns.Count(c => c.IsActive
                && (c.IsInDaoAnalysis || c.IsAddedByApi) && (c.PersistenceType == 'D' || c.PersistenceType == 'B')),
            SelectedForLoadCount = t.Columns.Count(c => c.IsActive && c.IsSelectedForLoad),
            UnreviewedCount = t.Columns.Count(c => c.IsActive
                && !c.IsInDaoAnalysis && !c.IsAddedByApi && !c.IsSelectedForLoad)
        }).OrderBy(t => t.DatabaseName).ThenBy(t => t.SchemaName).ThenBy(t => t.TableName)
          .ToListAsync();
    }

    public Task<SourceTable?> GetTableByIdAsync(int tableId)
        => _db.SourceTables
            .Include(t => t.Columns.OrderBy(c => c.SortOrder))
            .Include(t => t.Database).ThenInclude(d => d.Source)
            .FirstOrDefaultAsync(x => x.TableId == tableId);

    public async Task<SourceTable> AddTableAsync(SourceTable table)
    {
        _db.SourceTables.Add(table);
        await _db.SaveChangesAsync();
        return table;
    }

    public async Task UpdateTableAsync(SourceTable table)
    {
        _db.SourceTables.Update(table);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteTableAsync(int tableId)
    {
        var entity = await _db.SourceTables.FindAsync(tableId);
        if (entity is null) return;
        _db.SourceTables.Remove(entity);
        await _db.SaveChangesAsync();
    }

    // ── Columns ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceColumnInfo>> GetColumnsAsync(
        int? tableId = null, int? databaseId = null, int? sourceId = null,
        ColumnFilter filter = ColumnFilter.All, bool includeInactive = false)
    {
        var q = _db.SourceColumns
            .Include(c => c.Table)
                .ThenInclude(t => t.Database)
                    .ThenInclude(d => d.Source)
            .AsQueryable();

        if (!includeInactive) q = q.Where(c => c.IsActive);
        if (tableId.HasValue) q = q.Where(c => c.TableId == tableId.Value);
        if (databaseId.HasValue) q = q.Where(c => c.Table.DatabaseId == databaseId.Value);
        if (sourceId.HasValue) q = q.Where(c => c.Table.Database.SourceId == sourceId.Value);

        q = filter switch
        {
            ColumnFilter.InScopeRelational =>
                q.Where(c => (c.IsInDaoAnalysis || c.IsAddedByApi) && (c.PersistenceType == 'R' || c.PersistenceType == 'B')),
            ColumnFilter.InScopeDocument =>
                q.Where(c => (c.IsInDaoAnalysis || c.IsAddedByApi) && (c.PersistenceType == 'D' || c.PersistenceType == 'B')),
            ColumnFilter.SelectedForLoad =>
                q.Where(c => c.IsSelectedForLoad),
            _ => q
        };

        return await q.OrderBy(c => c.Table.Database.Source.ServerName)
            .ThenBy(c => c.Table.Database.DatabaseName)
            .ThenBy(c => c.Table.SchemaName)
            .ThenBy(c => c.Table.TableName)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.ColumnName)
            .Select(c => new SourceColumnInfo
            {
                ColumnId = c.ColumnId,
                TableId = c.TableId,
                ServerName = c.Table.Database.Source.ServerName,
                DatabaseName = c.Table.Database.DatabaseName,
                SchemaName = c.Table.SchemaName,
                TableName = c.Table.TableName,
                ColumnName = c.ColumnName,
                PersistenceType = c.PersistenceType,
                IsInDaoAnalysis = c.IsInDaoAnalysis,
                IsAddedByApi = c.IsAddedByApi,
                IsSelectedForLoad = c.IsSelectedForLoad,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                ModifiedAt = c.ModifiedAt,
                ModifiedBy = c.ModifiedBy
            }).ToListAsync();
    }

    public Task<SourceColumn?> GetColumnByIdAsync(int columnId)
        => _db.SourceColumns
            .Include(c => c.Table).ThenInclude(t => t.Database).ThenInclude(d => d.Source)
            .FirstOrDefaultAsync(c => c.ColumnId == columnId);

    public async Task<SourceColumn> AddColumnAsync(SourceColumn column)
    {
        _db.SourceColumns.Add(column);
        await _db.SaveChangesAsync();
        return column;
    }

    public async Task UpdateColumnAsync(SourceColumn column)
    {
        _db.SourceColumns.Update(column);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteColumnAsync(int columnId)
    {
        var entity = await _db.SourceColumns.FindAsync(columnId);
        if (entity is null) return;
        _db.SourceColumns.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task BulkUpdateColumnsAsync(IEnumerable<int> columnIds, Action<SourceColumn> updateAction)
    {
        var ids = columnIds.ToList();
        var columns = await _db.SourceColumns.Where(c => ids.Contains(c.ColumnId)).ToListAsync();
        foreach (var col in columns) updateAction(col);
        await _db.SaveChangesAsync();
    }

    // ── Summary ─────────────────────────────────────────────────────────────

    public async Task<CatalogueSummaryDto> GetCatalogueSummaryAsync()
    {
        var lastCol = await _db.SourceColumns
            .Where(c => c.ModifiedAt.HasValue)
            .OrderByDescending(c => c.ModifiedAt)
            .FirstOrDefaultAsync();

        return new CatalogueSummaryDto
        {
            TotalServers = await _db.Sources.CountAsync(s => s.IsActive),
            TotalDatabases = await _db.SourceDatabases.CountAsync(d => d.IsActive),
            TotalTables = await _db.SourceTables.CountAsync(t => t.IsActive),
            TotalColumns = await _db.SourceColumns.CountAsync(c => c.IsActive),
            InScopeRelationalColumns = await _db.SourceColumns.CountAsync(c =>
                c.IsActive && (c.IsInDaoAnalysis || c.IsAddedByApi) && c.PersistenceType == 'R'),
            InScopeDocumentColumns = await _db.SourceColumns.CountAsync(c =>
                c.IsActive && (c.IsInDaoAnalysis || c.IsAddedByApi) && c.PersistenceType == 'D'),
            SelectedForLoadColumns = await _db.SourceColumns.CountAsync(c => c.IsActive && c.IsSelectedForLoad),
            UnreviewedColumns = await _db.SourceColumns.CountAsync(c =>
                c.IsActive && !c.IsInDaoAnalysis && !c.IsAddedByApi && !c.IsSelectedForLoad),
            LastModifiedAt = lastCol?.ModifiedAt,
            LastModifiedBy = lastCol?.ModifiedBy
        };
    }

    // ── Uniqueness checks ────────────────────────────────────────────────────

    public Task<bool> ServerNameExistsAsync(string serverName, int? excludeSourceId = null)
        => _db.Sources.AnyAsync(s =>
            s.ServerName == serverName && (excludeSourceId == null || s.SourceId != excludeSourceId.Value));

    public Task<bool> DatabaseNameExistsAsync(int sourceId, string databaseName, int? excludeDatabaseId = null)
        => _db.SourceDatabases.AnyAsync(d =>
            d.SourceId == sourceId && d.DatabaseName == databaseName
            && (excludeDatabaseId == null || d.DatabaseId != excludeDatabaseId.Value));

    public Task<bool> TableNameExistsAsync(int databaseId, string schemaName, string tableName, int? excludeTableId = null)
        => _db.SourceTables.AnyAsync(t =>
            t.DatabaseId == databaseId && t.SchemaName == schemaName && t.TableName == tableName
            && (excludeTableId == null || t.TableId != excludeTableId.Value));

    public Task<bool> ColumnNameExistsAsync(int tableId, string columnName, int? excludeColumnId = null)
        => _db.SourceColumns.AnyAsync(c =>
            c.TableId == tableId && c.ColumnName == columnName
            && (excludeColumnId == null || c.ColumnId != excludeColumnId.Value));
}
