namespace DataManager.Core.Models.Entities;

public class Server
{
    public int ServerId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ServerRole Role { get; set; } = ServerRole.Source;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public ICollection<SourceDatabase> Databases { get; set; } = new List<SourceDatabase>();
    public ICollection<TargetDatabase> TargetDatabases { get; set; } = new List<TargetDatabase>();
    public ServerConnection? Connection { get; set; }
}
