using DataManager.Core.DTOs;
using DataManager.Core.Interfaces;
using DataManager.Core.Models.Entities;
using DataManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DataManager.Infrastructure.Repositories;

public class EfDataManagerRepository : IDataManagerRepository
{
    private readonly DataManagerDbContext _db;

    public EfDataManagerRepository(DataManagerDbContext db)
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
                .Count(c => c.IsActive && c.IsSelectedForLoad),
            LastImportedModelHash = d.LastImportedModelHash
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

    public async Task<DataManagerSummaryDto> GetDataManagerSummaryAsync()
    {
        var lastCol = await _db.SourceColumns
            .Where(c => c.ModifiedAt.HasValue)
            .OrderByDescending(c => c.ModifiedAt)
            .FirstOrDefaultAsync();

        return new DataManagerSummaryDto
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

    // ── Views ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceViewSummary>> GetViewsAsync(int databaseId)
    {
        return await _db.SourceViews
            .Where(v => v.DatabaseId == databaseId && v.IsActive)
            .OrderBy(v => v.SchemaName).ThenBy(v => v.ViewName)
            .Select(v => new SourceViewSummary
            {
                SourceViewId = v.SourceViewId,
                DatabaseId = v.DatabaseId,
                SchemaName = v.SchemaName,
                ViewName = v.ViewName,
                ColumnCount = v.Columns.Count,
                HasSqlBody = v.SqlBody != null,
                IsActive = v.IsActive
            }).ToListAsync();
    }

    public async Task<SourceViewDetail?> GetViewByIdAsync(int viewId)
    {
        var v = await _db.SourceViews
            .Include(x => x.Columns.OrderBy(c => c.OrdinalPosition))
            .FirstOrDefaultAsync(x => x.SourceViewId == viewId);
        if (v is null) return null;
        return new SourceViewDetail
        {
            SourceViewId = v.SourceViewId,
            DatabaseId = v.DatabaseId,
            SchemaName = v.SchemaName,
            ViewName = v.ViewName,
            SqlBody = v.SqlBody,
            HasStandardAuditColumns = v.HasStandardAuditColumns,
            IsActive = v.IsActive,
            Columns = v.Columns.Select(c => new SourceViewColumnDto
            {
                SourceViewColumnId = c.SourceViewColumnId,
                ColumnName = c.ColumnName,
                SqlType = c.SqlType,
                IsNullable = c.IsNullable,
                MaxLength = c.MaxLength,
                Precision = c.Precision,
                Scale = c.Scale,
                OrdinalPosition = c.OrdinalPosition
            }).ToList()
        };
    }

    // ── Stored Procedures ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceStoredProcedureSummary>> GetStoredProceduresAsync(int databaseId)
    {
        return await _db.SourceStoredProcedures
            .Where(p => p.DatabaseId == databaseId && p.IsActive)
            .OrderBy(p => p.SchemaName).ThenBy(p => p.ProcedureName)
            .Select(p => new SourceStoredProcedureSummary
            {
                SourceStoredProcedureId = p.SourceStoredProcedureId,
                DatabaseId = p.DatabaseId,
                SchemaName = p.SchemaName,
                ProcedureName = p.ProcedureName,
                ParameterCount = p.Parameters.Count,
                HasSqlBody = p.SqlBody != null,
                IsActive = p.IsActive
            }).ToListAsync();
    }

    public async Task<SourceStoredProcedureDetail?> GetStoredProcedureByIdAsync(int id)
    {
        var p = await _db.SourceStoredProcedures
            .Include(x => x.Parameters)
            .FirstOrDefaultAsync(x => x.SourceStoredProcedureId == id);
        if (p is null) return null;
        return new SourceStoredProcedureDetail
        {
            SourceStoredProcedureId = p.SourceStoredProcedureId,
            DatabaseId = p.DatabaseId,
            SchemaName = p.SchemaName,
            ProcedureName = p.ProcedureName,
            SqlBody = p.SqlBody,
            IsActive = p.IsActive,
            Parameters = p.Parameters.Select(x => new SourceStoredProcedureParameterDto
            {
                SourceStoredProcedureParameterId = x.SourceStoredProcedureParameterId,
                Name = x.Name,
                SqlType = x.SqlType,
                IsOutput = x.IsOutput,
                DefaultValue = x.DefaultValue
            }).ToList()
        };
    }

    // ── Functions ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceFunctionSummary>> GetFunctionsAsync(int databaseId)
    {
        return await _db.SourceFunctions
            .Where(f => f.DatabaseId == databaseId && f.IsActive)
            .OrderBy(f => f.SchemaName).ThenBy(f => f.FunctionName)
            .Select(f => new SourceFunctionSummary
            {
                SourceFunctionId = f.SourceFunctionId,
                DatabaseId = f.DatabaseId,
                SchemaName = f.SchemaName,
                FunctionName = f.FunctionName,
                FunctionType = f.FunctionType,
                ReturnType = f.ReturnType,
                HasSqlBody = f.SqlBody != null,
                IsActive = f.IsActive
            }).ToListAsync();
    }

    public async Task<SourceFunctionDetail?> GetFunctionByIdAsync(int id)
    {
        var f = await _db.SourceFunctions.FirstOrDefaultAsync(x => x.SourceFunctionId == id);
        if (f is null) return null;
        return new SourceFunctionDetail
        {
            SourceFunctionId = f.SourceFunctionId,
            DatabaseId = f.DatabaseId,
            SchemaName = f.SchemaName,
            FunctionName = f.FunctionName,
            FunctionType = f.FunctionType,
            ReturnType = f.ReturnType,
            SqlBody = f.SqlBody,
            IsActive = f.IsActive
        };
    }

    // ── Triggers ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceTriggerSummary>> GetTriggersAsync(int tableId)
    {
        return await _db.SourceTriggers
            .Where(t => t.TableId == tableId && t.IsActive)
            .OrderBy(t => t.TriggerName)
            .Select(t => new SourceTriggerSummary
            {
                SourceTriggerId = t.SourceTriggerId,
                TableId = t.TableId,
                SchemaName = t.SchemaName,
                TriggerName = t.TriggerName,
                HasSqlBody = t.SqlBody != null,
                IsActive = t.IsActive
            }).ToListAsync();
    }

    public async Task<IReadOnlyList<SourceTriggerDetail>> GetTriggerDetailsAsync(int tableId)
    {
        return await _db.SourceTriggers
            .Where(t => t.TableId == tableId && t.IsActive)
            .OrderBy(t => t.TriggerName)
            .Select(t => new SourceTriggerDetail
            {
                SourceTriggerId = t.SourceTriggerId,
                TableId = t.TableId,
                SchemaName = t.SchemaName,
                TriggerName = t.TriggerName,
                SqlBody = t.SqlBody,
                IsActive = t.IsActive
            }).ToListAsync();
    }

    // ── Indexes ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceIndexSummary>> GetIndexesAsync(int tableId)
    {
        return await _db.SourceIndexes
            .Include(i => i.Columns)
            .Where(i => i.TableId == tableId && i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new SourceIndexSummary
            {
                SourceIndexId = i.SourceIndexId,
                TableId = i.TableId,
                Name = i.Name,
                IsUnique = i.IsUnique,
                IsClustered = i.IsClustered,
                IsPrimaryKeyIndex = i.IsPrimaryKeyIndex,
                FilterDefinition = i.FilterDefinition,
                Columns = i.Columns.OrderBy(c => c.SourceIndexColumnId).Select(c => c.ColumnName).ToList()
            }).ToListAsync();
    }

    // ── Foreign Keys ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceForeignKeySummary>> GetForeignKeysAsync(int tableId)
    {
        return await _db.SourceForeignKeys
            .Include(fk => fk.Columns.OrderBy(c => c.Ordinal))
            .Where(fk => fk.TableId == tableId && fk.IsActive)
            .OrderBy(fk => fk.Name)
            .Select(fk => new SourceForeignKeySummary
            {
                SourceForeignKeyId = fk.SourceForeignKeyId,
                TableId = fk.TableId,
                Name = fk.Name,
                ToSchema = fk.ToSchema,
                ToTable = fk.ToTable,
                OnDeleteCascade = fk.OnDeleteCascade,
                Cardinality = fk.Cardinality,
                Columns = fk.Columns.OrderBy(c => c.Ordinal).Select(c => new SourceForeignKeyColumnDto
                {
                    FromColumn = c.FromColumn,
                    ToColumn = c.ToColumn,
                    Ordinal = c.Ordinal
                }).ToList()
            }).ToListAsync();
    }

    // ── Check Constraints ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceCheckConstraintSummary>> GetCheckConstraintsAsync(int tableId)
    {
        return await _db.SourceCheckConstraints
            .Where(c => c.TableId == tableId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SourceCheckConstraintSummary
            {
                SourceCheckConstraintId = c.SourceCheckConstraintId,
                TableId = c.TableId,
                Name = c.Name,
                Expression = c.Expression,
                IsActive = c.IsActive
            }).ToListAsync();
    }

    // ── Unique Constraints ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<SourceUniqueConstraintSummary>> GetUniqueConstraintsAsync(int tableId)
    {
        return await _db.SourceUniqueConstraints
            .Include(uc => uc.Columns)
            .Where(uc => uc.TableId == tableId && uc.IsActive)
            .OrderBy(uc => uc.Name)
            .Select(uc => new SourceUniqueConstraintSummary
            {
                SourceUniqueConstraintId = uc.SourceUniqueConstraintId,
                TableId = uc.TableId,
                Name = uc.Name,
                IsClustered = uc.IsClustered,
                Columns = uc.Columns.Select(c => c.ColumnName).ToList(),
                IsActive = uc.IsActive
            }).ToListAsync();
    }

    // ── Schema import ────────────────────────────────────────────────────────

    public async Task UpdateDatabaseHashAsync(int databaseId, string modelHash)
    {
        var db = await _db.SourceDatabases.FindAsync(databaseId)
            ?? throw new InvalidOperationException($"SourceDatabase {databaseId} not found.");

        db.LastImportedModelHash = modelHash;
        db.LastImportedAt = DateTime.UtcNow;
        db.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSchemaForDatabaseAsync(int databaseId)
    {
        // Delete all schema objects for the given database — views, procs, functions
        var views = _db.SourceViews.Where(v => v.DatabaseId == databaseId);
        _db.SourceViews.RemoveRange(views);

        var procs = _db.SourceStoredProcedures.Where(p => p.DatabaseId == databaseId);
        _db.SourceStoredProcedures.RemoveRange(procs);

        var funcs = _db.SourceFunctions.Where(f => f.DatabaseId == databaseId);
        _db.SourceFunctions.RemoveRange(funcs);

        // Delete table-level schema objects (indexes, FKs, constraints, triggers)
        var tableIds = await _db.SourceTables
            .Where(t => t.DatabaseId == databaseId)
            .Select(t => t.TableId)
            .ToListAsync();

        foreach (var tableId in tableIds)
        {
            _db.SourceIndexes.RemoveRange(_db.SourceIndexes.Where(i => i.TableId == tableId));
            _db.SourceForeignKeys.RemoveRange(_db.SourceForeignKeys.Where(fk => fk.TableId == tableId));
            _db.SourceCheckConstraints.RemoveRange(_db.SourceCheckConstraints.Where(c => c.TableId == tableId));
            _db.SourceUniqueConstraints.RemoveRange(_db.SourceUniqueConstraints.Where(u => u.TableId == tableId));
            _db.SourceTriggers.RemoveRange(_db.SourceTriggers.Where(t => t.TableId == tableId));
        }

        await _db.SaveChangesAsync();
    }

    public async Task BulkInsertViewsAsync(IEnumerable<SourceView> views)
    {
        _db.SourceViews.AddRange(views);
        await _db.SaveChangesAsync();
    }

    public async Task BulkInsertStoredProceduresAsync(IEnumerable<SourceStoredProcedure> procedures)
    {
        _db.SourceStoredProcedures.AddRange(procedures);
        await _db.SaveChangesAsync();
    }

    public async Task BulkInsertFunctionsAsync(IEnumerable<SourceFunction> functions)
    {
        _db.SourceFunctions.AddRange(functions);
        await _db.SaveChangesAsync();
    }

    public async Task BulkInsertTableSchemaAsync(int tableId, IEnumerable<SourceIndex> indexes,
        IEnumerable<SourceForeignKey> foreignKeys, IEnumerable<SourceCheckConstraint> checkConstraints,
        IEnumerable<SourceUniqueConstraint> uniqueConstraints, IEnumerable<SourceTrigger> triggers)
    {
        _db.SourceIndexes.AddRange(indexes);
        _db.SourceForeignKeys.AddRange(foreignKeys);
        _db.SourceCheckConstraints.AddRange(checkConstraints);
        _db.SourceUniqueConstraints.AddRange(uniqueConstraints);
        _db.SourceTriggers.AddRange(triggers);
        await _db.SaveChangesAsync();
    }
}
