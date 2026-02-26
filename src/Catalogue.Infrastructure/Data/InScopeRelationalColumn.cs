namespace Catalogue.Infrastructure.Data;

/// <summary>Keyless entity representing vw_InScopeRelationalColumns.</summary>
public class InScopeRelationalColumn
{
    public int ColumnId { get; set; }
    public int TableId { get; set; }
    public int DatabaseId { get; set; }
    public int SourceId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public char PersistenceType { get; set; }
    public bool IsInDaoAnalysis { get; set; }
    public bool IsAddedByApi { get; set; }
    public bool IsSelectedForLoad { get; set; }
    public int SortOrder { get; set; }
}
