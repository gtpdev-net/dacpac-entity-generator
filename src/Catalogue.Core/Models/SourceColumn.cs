namespace Catalogue.Core.Models;

public class SourceColumn
{
    public int ColumnId { get; set; }
    public int TableId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    /// <summary>'R' = Relational, 'D' = Document, 'B' = Both</summary>
    public char PersistenceType { get; set; } = 'R';
    public bool IsInDaoAnalysis { get; set; }
    public bool IsAddedByApi { get; set; }
    public bool IsSelectedForLoad { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
}
