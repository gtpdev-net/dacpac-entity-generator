namespace Catalogue.Core.Models.Schema;

public class SourceViewColumn
{
    public int SourceViewColumnId { get; set; }
    public int SourceViewId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string? SqlType { get; set; }
    public bool IsNullable { get; set; } = true;
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public int OrdinalPosition { get; set; }

    public SourceView View { get; set; } = null!;
}
