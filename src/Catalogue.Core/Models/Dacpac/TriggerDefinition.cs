namespace Catalogue.Core.Models.Dacpac;

public class TriggerDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public string ParentSchema { get; set; } = string.Empty;
    public string ParentTable { get; set; } = string.Empty;
}
