namespace DataManager.Core.Models.Entities;

public class TargetDatabase
{
    public int TargetDatabaseId { get; set; }
    public int ServerId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public Server Server { get; set; } = null!;
}
