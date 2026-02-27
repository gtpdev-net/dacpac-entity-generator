namespace Catalogue.Core.Models.Entities;

public class SourceFunction
{
    public int SourceFunctionId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public string FunctionName { get; set; } = string.Empty;
    /// <summary>e.g. 'Scalar', 'TableValued', 'InlineTableValued'</summary>
    public string? FunctionType { get; set; }
    public string? ReturnType { get; set; }
    public string? SqlBody { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceDatabase Database { get; set; } = null!;
}
