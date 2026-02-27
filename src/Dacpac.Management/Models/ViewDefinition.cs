namespace Dacpac.Management.Models;

public class ViewDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = new();
    public bool HasStandardAuditColumns { get; set; }
    public string? SqlBody { get; set; }
}
