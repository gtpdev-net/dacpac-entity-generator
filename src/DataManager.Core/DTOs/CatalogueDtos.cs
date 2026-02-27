namespace DataManager.Core.DTOs;

public class SourceDatabaseInfo
{
    public int DatabaseId { get; set; }
    public int SourceId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int TableCount { get; set; }
    public int InScopeColumnCount { get; set; }
    public int SelectedForLoadCount { get; set; }
}

public class SourceTableInfo
{
    public int TableId { get; set; }
    public int DatabaseId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public long? EstimatedRowCount { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int TotalColumnCount { get; set; }
    public int InScopeRelationalCount { get; set; }
    public int InScopeDocumentCount { get; set; }
    public int SelectedForLoadCount { get; set; }
    public int UnreviewedCount { get; set; }
}

public class SourceColumnInfo
{
    public int ColumnId { get; set; }
    public int TableId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public char PersistenceType { get; set; } = 'R';
    public bool IsInDaoAnalysis { get; set; }
    public bool IsAddedByApi { get; set; }
    public bool IsSelectedForLoad { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }
}

public class DataManagerSummaryDto
{
    public int TotalServers { get; set; }
    public int TotalDatabases { get; set; }
    public int TotalTables { get; set; }
    public int TotalColumns { get; set; }
    public int InScopeRelationalColumns { get; set; }
    public int InScopeDocumentColumns { get; set; }
    public int SelectedForLoadColumns { get; set; }
    public int UnreviewedColumns { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public class ImportPreviewRow
{
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public char PersistenceType { get; set; } = 'R';
    public bool IsInDaoAnalysis { get; set; }
    public bool IsAddedByApi { get; set; }
    public bool IsSelectedForLoad { get; set; }
    public long? NumberOfRecords { get; set; }
    public string? Warning { get; set; }
}

public class ImportResultDto
{
    public int TablesAdded { get; set; }
    public int ColumnsAdded { get; set; }
    public int ColumnsUpdated { get; set; }
    public int ColumnsRemoved { get; set; }
    public int ColumnsSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

// ── Schema DTOs ───────────────────────────────────────────────────────────────

public class SourceViewSummary
{
    public int SourceViewId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
    public bool HasSqlBody { get; set; }
    public bool IsActive { get; set; }
}

public class SourceViewDetail
{
    public int SourceViewId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public bool HasStandardAuditColumns { get; set; }
    public bool IsActive { get; set; }
    public List<SourceViewColumnDto> Columns { get; set; } = new();
}

public class SourceViewColumnDto
{
    public int SourceViewColumnId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string? SqlType { get; set; }
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public int OrdinalPosition { get; set; }
}

public class SourceStoredProcedureSummary
{
    public int SourceStoredProcedureId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    public bool HasSqlBody { get; set; }
    public bool IsActive { get; set; }
}

public class SourceStoredProcedureDetail
{
    public int SourceStoredProcedureId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ProcedureName { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public bool IsActive { get; set; }
    public List<SourceStoredProcedureParameterDto> Parameters { get; set; } = new();
}

public class SourceStoredProcedureParameterDto
{
    public int SourceStoredProcedureParameterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SqlType { get; set; }
    public bool IsOutput { get; set; }
    public string? DefaultValue { get; set; }
}

public class SourceFunctionSummary
{
    public int SourceFunctionId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string? FunctionType { get; set; }
    public string? ReturnType { get; set; }
    public bool HasSqlBody { get; set; }
    public bool IsActive { get; set; }
}

public class SourceFunctionDetail
{
    public int SourceFunctionId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string? FunctionType { get; set; }
    public string? ReturnType { get; set; }
    public string? SqlBody { get; set; }
    public bool IsActive { get; set; }
}

public class SourceTriggerSummary
{
    public int SourceTriggerId { get; set; }
    public int TableId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public bool HasSqlBody { get; set; }
    public bool IsActive { get; set; }
}

public class SourceTriggerDetail
{
    public int SourceTriggerId { get; set; }
    public int TableId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public bool IsActive { get; set; }
}

public class SourceIndexSummary
{
    public int SourceIndexId { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public bool IsPrimaryKeyIndex { get; set; }
    public string? FilterDefinition { get; set; }
    public List<string> Columns { get; set; } = new();
}

public class SourceForeignKeySummary
{
    public int SourceForeignKeyId { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public bool OnDeleteCascade { get; set; }
    public string? Cardinality { get; set; }
    public List<SourceForeignKeyColumnDto> Columns { get; set; } = new();
}

public class SourceForeignKeyColumnDto
{
    public string FromColumn { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public int Ordinal { get; set; }
}

public class SchemaImportResultDto
{
    public bool Success { get; set; }
    public int TablesImported { get; set; }
    public int ViewsImported { get; set; }
    public int StoredProceduresImported { get; set; }
    public int FunctionsImported { get; set; }
    public int TriggersImported { get; set; }
    public int IndexesImported { get; set; }
    public int ForeignKeysImported { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

