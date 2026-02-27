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

    // --- Views ---
    Task<IReadOnlyList<SourceViewSummary>> GetViewsAsync(int databaseId);
    Task<SourceViewDetail?> GetViewByIdAsync(int viewId);

    // --- Stored Procedures ---
    Task<IReadOnlyList<SourceStoredProcedureSummary>> GetStoredProceduresAsync(int databaseId);
    Task<SourceStoredProcedureDetail?> GetStoredProcedureByIdAsync(int id);

    // --- Functions ---
    Task<IReadOnlyList<SourceFunctionSummary>> GetFunctionsAsync(int databaseId);
    Task<SourceFunctionDetail?> GetFunctionByIdAsync(int id);

    // --- Triggers ---
    Task<IReadOnlyList<SourceTriggerSummary>> GetTriggersAsync(int tableId);
    Task<IReadOnlyList<SourceTriggerDetail>> GetTriggerDetailsAsync(int tableId);

    // --- Indexes ---
    Task<IReadOnlyList<SourceIndexSummary>> GetIndexesAsync(int tableId);

    // --- Foreign Keys ---
    Task<IReadOnlyList<SourceForeignKeySummary>> GetForeignKeysAsync(int tableId);

    // --- Schema import (bulk operations) ---
    Task DeleteSchemaForDatabaseAsync(int databaseId);
    Task BulkInsertViewsAsync(IEnumerable<SourceView> views);
    Task BulkInsertStoredProceduresAsync(IEnumerable<SourceStoredProcedure> procedures);
    Task BulkInsertFunctionsAsync(IEnumerable<SourceFunction> functions);
    Task BulkInsertTableSchemaAsync(int tableId, IEnumerable<SourceIndex> indexes,
        IEnumerable<SourceForeignKey> foreignKeys, IEnumerable<SourceCheckConstraint> checkConstraints,
        IEnumerable<SourceUniqueConstraint> uniqueConstraints, IEnumerable<SourceTrigger> triggers);
}

