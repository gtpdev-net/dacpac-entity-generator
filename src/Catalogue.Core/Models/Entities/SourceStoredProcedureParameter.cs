namespace Catalogue.Core.Models.Entities;

public class SourceStoredProcedureParameter
{
    public int SourceStoredProcedureParameterId { get; set; }
    public int SourceStoredProcedureId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SqlType { get; set; }
    public bool IsOutput { get; set; }
    public string? DefaultValue { get; set; }

    public SourceStoredProcedure StoredProcedure { get; set; } = null!;
}
