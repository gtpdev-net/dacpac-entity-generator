namespace Catalogue.Core.Models;

public class SourceStoredProcedure
{
    public int SourceStoredProcedureId { get; set; }
    public int DatabaseId { get; set; }
    public string SchemaName { get; set; } = "dbo";
    public string ProcedureName { get; set; } = string.Empty;
    public string? SqlBody { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceDatabase Database { get; set; } = null!;
    public ICollection<SourceStoredProcedureParameter> Parameters { get; set; } = new List<SourceStoredProcedureParameter>();
}
