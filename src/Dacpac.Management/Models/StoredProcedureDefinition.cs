namespace Dacpac.Management.Models;

public class StoredProcedureDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public List<ParameterDefinition> Parameters { get; set; } = new();
}
