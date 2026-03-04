namespace DataManager.Core.Models.Entities;

public class MigrationConfig
{
    public int MigrationConfigId { get; set; }
    public int TableId { get; set; }

    public string SourceServer { get; set; } = string.Empty;
    public string SourceDatabase { get; set; } = string.Empty;
    public string SourceSchema { get; set; } = string.Empty;
    public string SourceTableName { get; set; } = string.Empty;

    public string? DestinationServer { get; set; }
    public string? DestinationDatabase { get; set; }
    public string DestinationSchema { get; set; } = string.Empty;
    public string DestinationTable { get; set; } = string.Empty;

    public string ColumnList { get; set; } = string.Empty;
    public string? FilterCondition { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public SourceTable Table { get; set; } = null!;
}
