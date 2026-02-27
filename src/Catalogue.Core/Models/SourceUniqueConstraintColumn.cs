namespace Catalogue.Core.Models;

public class SourceUniqueConstraintColumn
{
    public int SourceUniqueConstraintColumnId { get; set; }
    public int SourceUniqueConstraintId { get; set; }
    public string ColumnName { get; set; } = string.Empty;

    public SourceUniqueConstraint UniqueConstraint { get; set; } = null!;
}
