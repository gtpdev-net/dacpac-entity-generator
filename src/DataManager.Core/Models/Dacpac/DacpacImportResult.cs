namespace DataManager.Core.Models.Dacpac;

/// <summary>
/// Result of a single DACPAC schema import operation.
/// </summary>
public class DacpacImportResult
{
    public bool Success { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public int TablesProcessed { get; set; }
    public int ColumnsUpdated { get; set; }
    public int ColumnsCreated { get; set; }
    public int ViewsImported { get; set; }
    public int StoredProceduresImported { get; set; }
    public int FunctionsImported { get; set; }
    public int IndexesImported { get; set; }
    public int ForeignKeysImported { get; set; }
    public int TriggersImported { get; set; }
    public List<string> Errors { get; set; } = new();

    /// <summary>True when the import was skipped because the DACPAC model is identical to the last import.</summary>
    public bool WasSkipped { get; set; }

    /// <summary>Human-readable reason the import was skipped, if <see cref="WasSkipped"/> is true.</summary>
    public string? SkipReason { get; set; }
}
