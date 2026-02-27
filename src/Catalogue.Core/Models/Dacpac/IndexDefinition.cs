namespace Catalogue.Core.Models.Dacpac;

public class IndexDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public bool IsUnique { get; set; }
    public bool IsClustered { get; set; }
    public bool IsPrimaryKeyIndex { get; set; }
    public List<string> IncludedColumns { get; set; } = new();
    public Dictionary<string, bool> ColumnSortOrder { get; set; } = new(); // true = ASC, false = DESC
    public string? FilterDefinition { get; set; }
}
