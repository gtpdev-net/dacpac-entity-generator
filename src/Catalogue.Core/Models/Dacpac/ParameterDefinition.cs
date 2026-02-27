namespace Catalogue.Core.Models.Dacpac;

public class ParameterDefinition
{
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public bool IsOutput { get; set; }
    public string? DefaultValue { get; set; }
}
