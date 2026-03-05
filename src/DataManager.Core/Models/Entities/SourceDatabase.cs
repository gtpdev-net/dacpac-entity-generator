namespace DataManager.Core.Models.Entities;

public class SourceDatabase
{
    public int DatabaseId { get; set; }
    public int ServerId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// SHA-256 hex digest of the <c>model.xml</c> content from the most recent successful import.
    /// Used to skip re-importing an unchanged DACPAC file.
    /// </summary>
    public string? LastImportedModelHash { get; set; }

    /// <summary>UTC timestamp of the most recent successful import that wrote to this database.</summary>
    public DateTime? LastImportedAt { get; set; }

    public Server Server { get; set; } = null!;
    public ICollection<SourceTable> Tables { get; set; } = new List<SourceTable>();

    // ── Schema navigation properties ──────────────────────────────────────────
    public ICollection<SourceView> Views { get; set; } = new List<SourceView>();
    public ICollection<SourceStoredProcedure> StoredProcedures { get; set; } = new List<SourceStoredProcedure>();
    public ICollection<SourceFunction> Functions { get; set; } = new List<SourceFunction>();
}

