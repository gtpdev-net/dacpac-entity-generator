namespace DataManager.Core.Models.Entities;

public class SourceColumn
{
    public int ColumnId { get; set; }
    public int TableId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    /// <summary>'R' = Relational, 'D' = Document, 'B' = Both</summary>
    public char PersistenceType { get; set; } = 'R';
    public bool IsInDaoAnalysis { get; set; }
    public bool IsAddedByApi { get; set; }
    public bool IsSelectedForLoad { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    // ── Schema metadata (populated by DacpacSchemaImportService) ──────────────
    public string? SqlType { get; set; }
    public bool IsNullable { get; set; } = true;
    public int? MaxLength { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsPrimaryKey { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsComputed { get; set; }
    public bool IsComputedPersisted { get; set; }
    public string? ComputedExpression { get; set; }
    public bool IsRowVersion { get; set; }
    public bool IsConcurrencyToken { get; set; }
    public string? Collation { get; set; }
    public string? Description { get; set; }

    public SourceTable Table { get; set; } = null!;
}
