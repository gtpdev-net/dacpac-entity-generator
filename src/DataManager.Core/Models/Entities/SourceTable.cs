namespace DataManager.Core.Models.Entities;

public class SourceTable
{
    public int TableId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public string TableName { get; set; } = string.Empty;
    public long? EstimatedRowCount { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceDatabase Database { get; set; } = null!;
    public ICollection<SourceColumn> Columns { get; set; } = new List<SourceColumn>();

    // ── Schema navigation properties ──────────────────────────────────────────
    public ICollection<SourceIndex> Indexes { get; set; } = new List<SourceIndex>();
    public ICollection<SourceForeignKey> ForeignKeys { get; set; } = new List<SourceForeignKey>();
    public ICollection<SourceCheckConstraint> CheckConstraints { get; set; } = new List<SourceCheckConstraint>();
    public ICollection<SourceUniqueConstraint> UniqueConstraints { get; set; } = new List<SourceUniqueConstraint>();
    public ICollection<SourceTrigger> Triggers { get; set; } = new List<SourceTrigger>();
}

