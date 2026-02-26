namespace Catalogue.Core.Models;

public class Source
{
    public int SourceId { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public ICollection<SourceDatabase> Databases { get; set; } = new List<SourceDatabase>();
}
