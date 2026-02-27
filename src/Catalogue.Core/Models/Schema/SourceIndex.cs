namespace Catalogue.Core.Models.Schema;

public class SourceIndex
{
    public int SourceIndexId { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public bool IsPrimaryKeyIndex { get; set; }
    public string? FilterDefinition { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
    public ICollection<SourceIndexColumn> Columns { get; set; } = new List<SourceIndexColumn>();
}
