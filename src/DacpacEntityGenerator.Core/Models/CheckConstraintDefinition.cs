namespace DacpacEntityGenerator.Core.Models;

public class CheckConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public List<string> AffectedColumns { get; set; } = new();
}
