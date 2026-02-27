namespace Catalogue.Core.Models.Schema;

public class SourceCheckConstraint
{
    public int SourceCheckConstraintId { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
}
