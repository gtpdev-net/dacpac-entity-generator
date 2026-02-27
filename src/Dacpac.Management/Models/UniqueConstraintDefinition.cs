namespace Dacpac.Management.Models;

public class UniqueConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public bool IsClustered { get; set; }
}
