namespace Catalogue.Core.Models.Entities;

public class SourceIndexColumn
{
    public int SourceIndexColumnId { get; set; }
    public int SourceIndexId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    /// <summary>'ASC' or 'DESC'</summary>
    public string SortOrder { get; set; } = "ASC";
    public bool IsIncludedColumn { get; set; }

    public SourceIndex Index { get; set; } = null!;
}
