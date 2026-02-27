namespace Catalogue.Core.Models.Entities;

public class SourceUniqueConstraint
{
    public int SourceUniqueConstraintId { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsClustered { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
    public ICollection<SourceUniqueConstraintColumn> Columns { get; set; } = new List<SourceUniqueConstraintColumn>();
}
