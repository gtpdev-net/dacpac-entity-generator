namespace DataManager.Core.Models.Entities;

public class SourceDatabase
{
    public int DatabaseId { get; set; }
    public int SourceId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public Source Source { get; set; } = null!;
    public ICollection<SourceTable> Tables { get; set; } = new List<SourceTable>();

    // ── Schema navigation properties ──────────────────────────────────────────
    public ICollection<SourceView> Views { get; set; } = new List<SourceView>();
    public ICollection<SourceStoredProcedure> StoredProcedures { get; set; } = new List<SourceStoredProcedure>();
    public ICollection<SourceFunction> Functions { get; set; } = new List<SourceFunction>();
}

