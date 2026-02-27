namespace Catalogue.Core.Models.Entities;

public class SourceForeignKey
{
    public int SourceForeignKeyId { get; set; }
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public bool OnDeleteCascade { get; set; }
    public bool OnUpdateCascade { get; set; }
    /// <summary>e.g. 'ManyToOne', 'OneToOne'</summary>
    public string? Cardinality { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
    public ICollection<SourceForeignKeyColumn> Columns { get; set; } = new List<SourceForeignKeyColumn>();
}
