namespace DacpacEntityGenerator.Core.Models;

public class ForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> FromColumns { get; set; } = new();
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public List<string> ToColumns { get; set; } = new();
    public bool OnDeleteCascade { get; set; }
    public bool OnUpdateCascade { get; set; }
    public ForeignKeyCardinality Cardinality { get; set; }
}

public enum ForeignKeyCardinality
{
    Unknown,
    OneToMany,
    OneToOne,
    ManyToMany
}
