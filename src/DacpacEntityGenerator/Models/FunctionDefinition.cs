namespace DacpacEntityGenerator.Models;

public class FunctionDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public FunctionType Type { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public List<FunctionParameter> Parameters { get; set; } = new();
}

public enum FunctionType
{
    Scalar,
    TableValued,
    InlineTableValued
}

public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public bool IsOutput { get; set; }
}
