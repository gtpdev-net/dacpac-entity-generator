namespace DataManager.Core.Models.Entities;

public class CopyActivityLog
{
    public int LogId { get; set; }
    public string PipelineRunId { get; set; } = null!;
    public int MigrationConfigId { get; set; }
    public string SourceServer { get; set; } = null!;
    public string SourceDatabase { get; set; } = null!;
    public string SourceSchema { get; set; } = null!;
    public string SourceTable { get; set; } = null!;
    public string DestinationServer { get; set; } = null!;
    public string DestinationDatabase { get; set; } = null!;
    public string DestinationSchema { get; set; } = null!;
    public string DestinationTable { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public long RowsCopied { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationSeconds { get; set; }
}
