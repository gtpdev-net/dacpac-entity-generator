namespace Dacpac.Management.Models;

public class TableDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = new();
    public List<IndexDefinition> Indexes { get; set; } = new();
    public List<ForeignKeyDefinition> ForeignKeys { get; set; } = new();
    public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();
    public List<UniqueConstraintDefinition> UniqueConstraints { get; set; } = new();
}
