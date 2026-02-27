namespace Catalogue.Core.Models.Entities;

public class SourceForeignKeyColumn
{
    public int SourceForeignKeyColumnId { get; set; }
    public int SourceForeignKeyId { get; set; }
    public string FromColumn { get; set; } = string.Empty;
    public string ToColumn { get; set; } = string.Empty;
    public int Ordinal { get; set; }

    public SourceForeignKey ForeignKey { get; set; } = null!;
}
