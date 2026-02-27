namespace Catalogue.Core.Models.Schema;

public class SourceTrigger
{
    public int SourceTriggerId { get; set; }
    public int TableId { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public string TriggerName { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
}
