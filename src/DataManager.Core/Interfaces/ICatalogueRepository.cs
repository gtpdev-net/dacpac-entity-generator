using DataManager.Core.DTOs;
using DataManager.Core.Models.Entities;
using DataManager.Core.Models.StoredProcedures;

namespace DataManager.Core.Interfaces;

public enum ColumnFilter
{
    All,               // All active columns regardless of intent
    InScopeRelational, // (IsInDaoAnalysis OR IsAddedByApi) AND PersistenceType = 'R'
    InScopeDocument,   // (IsInDaoAnalysis OR IsAddedByApi) AND PersistenceType = 'D'
    SelectedForLoad    // IsSelectedForLoad = true
}

public interface IDataManagerRepository
{
    // --- Servers ---
    Task<IReadOnlyList<Server>> GetServersAsync(bool includeInactive = false);
    Task<IReadOnlyList<Server>> GetServersByRoleAsync(ServerRole role, bool includeInactive = false);
    Task<Server?> GetServerByIdAsync(int serverId);
    Task<Server> AddServerAsync(Server server);
    Task UpdateServerAsync(Server server);
    Task DeleteServerAsync(int serverId);

    // --- Server Connections ---
    Task<ServerConnection?> GetServerConnectionAsync(int serverId);
    Task SaveServerConnectionAsync(ServerConnection connection);

    // --- Target Databases ---
    Task<IReadOnlyList<TargetDatabase>> GetTargetDatabasesAsync(int? serverId = null, bool includeInactive = false);
    Task<TargetDatabase?> GetTargetDatabaseByIdAsync(int id);
    Task<TargetDatabase> AddTargetDatabaseAsync(TargetDatabase db);
    Task UpdateTargetDatabaseAsync(TargetDatabase db);
    Task DeleteTargetDatabaseAsync(int id);
    Task<bool> TargetDatabaseNameExistsAsync(int serverId, string name, int? excludeId = null);

    // --- Databases ---
    Task<IReadOnlyList<SourceDatabaseInfo>> GetInScopeDatabasesAsync(int? serverId = null, bool includeInactive = false);
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
        int? serverId = null,
        ColumnFilter filter = ColumnFilter.All,
        bool includeInactive = false);
    Task<SourceColumn?> GetColumnByIdAsync(int columnId);
    Task<SourceColumn> AddColumnAsync(SourceColumn column);
    Task UpdateColumnAsync(SourceColumn column);
    Task DeleteColumnAsync(int columnId);
    Task BulkUpdateColumnsAsync(IEnumerable<int> columnIds, Action<SourceColumn> updateAction);

    // --- Summary ---
    Task<DataManagerSummaryDto> GetDataManagerSummaryAsync();

    // --- Uniqueness checks ---
    Task<bool> ServerNameExistsAsync(string serverName, int? excludeServerId = null);
    Task<bool> DatabaseNameExistsAsync(int serverId, string databaseName, int? excludeDatabaseId = null);
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

    // --- Check Constraints ---
    Task<IReadOnlyList<SourceCheckConstraintSummary>> GetCheckConstraintsAsync(int tableId);

    // --- Unique Constraints ---
    Task<IReadOnlyList<SourceUniqueConstraintSummary>> GetUniqueConstraintsAsync(int tableId);

    // --- Schema import (bulk operations) ---
    Task UpdateDatabaseHashAsync(int databaseId, string modelHash);
    Task DeleteSchemaForDatabaseAsync(int databaseId);
    Task BulkInsertViewsAsync(IEnumerable<SourceView> views);
    Task BulkInsertStoredProceduresAsync(IEnumerable<SourceStoredProcedure> procedures);
    Task BulkInsertFunctionsAsync(IEnumerable<SourceFunction> functions);
    Task BulkInsertTableSchemaAsync(int tableId, IEnumerable<SourceIndex> indexes,
        IEnumerable<SourceForeignKey> foreignKeys, IEnumerable<SourceCheckConstraint> checkConstraints,
        IEnumerable<SourceUniqueConstraint> uniqueConstraints, IEnumerable<SourceTrigger> triggers);

    // --- MigrationConfig ---
    Task<IReadOnlyList<MigrationConfigInfo>> GetMigrationConfigsAsync(bool includeInactive = false);
    Task<MigrationConfig?> GetMigrationConfigByIdAsync(int id);
    Task<MigrationConfig> AddMigrationConfigAsync(MigrationConfig config);
    Task UpdateMigrationConfigAsync(MigrationConfig config);
    Task DeleteMigrationConfigAsync(int id);
    Task BulkUpsertMigrationConfigsAsync(IEnumerable<MigrationConfig> configs);

    // --- CopyActivityLog ---
    Task LogCopySuccessAsync(LogCopySuccessParams parameters);
    Task LogCopyFailureAsync(LogCopyFailureParams parameters);
}

