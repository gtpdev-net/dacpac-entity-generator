using Catalogue.Core.DTOs;
using Catalogue.Core.Models;

namespace Catalogue.Core.Interfaces;

public enum ColumnFilter
{
    All,               // All active columns regardless of intent
    InScopeRelational, // (IsInDaoAnalysis OR IsAddedByApi) AND PersistenceType = 'R'
    InScopeDocument,   // (IsInDaoAnalysis OR IsAddedByApi) AND PersistenceType = 'D'
    SelectedForLoad    // IsSelectedForLoad = true
}

public interface ICatalogueRepository
{
    // --- Sources ---
    Task<IReadOnlyList<Source>> GetSourcesAsync(bool includeInactive = false);
    Task<Source?> GetSourceByIdAsync(int sourceId);
    Task<Source> AddSourceAsync(Source source);
    Task UpdateSourceAsync(Source source);
    Task DeleteSourceAsync(int sourceId);

    // --- Databases ---
    Task<IReadOnlyList<SourceDatabaseInfo>> GetInScopeDatabasesAsync(int? sourceId = null, bool includeInactive = false);
    Task<SourceDatabase?> GetDatabaseByIdAsync(int databaseId);
    Task<SourceDatabase> AddDatabaseAsync(SourceDatabase database);
    Task UpdateDatabaseAsync(SourceDatabase database);
    Task DeleteDatabaseAsync(int databaseId);

    // --- Tables ---
    Task<IReadOnlyList<SourceTableInfo>> GetInScopeTablesAsync(int? databaseId = null, bool includeInactive = false);
    Task<SourceTable?> GetTableByIdAsync(int tableId);
    Task<SourceTable> AddTableAsync(SourceTable table);
    Task UpdateTableAsync(SourceTable table);
    Task DeleteTableAsync(int tableId);

    // --- Columns ---
    Task<IReadOnlyList<SourceColumnInfo>> GetColumnsAsync(
        int? tableId = null,
        int? databaseId = null,
        int? sourceId = null,
        ColumnFilter filter = ColumnFilter.All,
        bool includeInactive = false);
    Task<SourceColumn?> GetColumnByIdAsync(int columnId);
    Task<SourceColumn> AddColumnAsync(SourceColumn column);
    Task UpdateColumnAsync(SourceColumn column);
    Task DeleteColumnAsync(int columnId);
    Task BulkUpdateColumnsAsync(IEnumerable<int> columnIds, Action<SourceColumn> updateAction);

    // --- Summary ---
    Task<CatalogueSummaryDto> GetCatalogueSummaryAsync();

    // --- Uniqueness checks ---
    Task<bool> ServerNameExistsAsync(string serverName, int? excludeSourceId = null);
    Task<bool> DatabaseNameExistsAsync(int sourceId, string databaseName, int? excludeDatabaseId = null);
    Task<bool> TableNameExistsAsync(int databaseId, string schemaName, string tableName, int? excludeTableId = null);
    Task<bool> ColumnNameExistsAsync(int tableId, string columnName, int? excludeColumnId = null);
}
