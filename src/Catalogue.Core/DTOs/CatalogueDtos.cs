namespace Catalogue.Core.DTOs;

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

public class CatalogueSummaryDto
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
