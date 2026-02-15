namespace DacpacEntityGenerator.Models;

public class ColumnDefinition
{
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsFromExcel { get; set; } // Track if column came from Excel filter
    public int? Precision { get; set; }
    public int? Scale { get; set; }
}
