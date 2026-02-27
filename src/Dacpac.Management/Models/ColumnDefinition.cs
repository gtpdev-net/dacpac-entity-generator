namespace Dacpac.Management.Models;

public class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsFromExcel { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsComputed { get; set; }
    public bool IsComputedPersisted { get; set; }
    public string? ComputedExpression { get; set; }
    public bool IsRowVersion { get; set; }
    public bool IsConcurrencyToken { get; set; }
    public string? Collation { get; set; }
    public string? Description { get; set; }
}
