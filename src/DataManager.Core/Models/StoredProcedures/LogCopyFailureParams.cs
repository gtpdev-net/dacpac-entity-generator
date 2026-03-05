namespace DataManager.Core.Models.StoredProcedures;

public class LogCopyFailureParams
{
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
    public string ErrorMessage { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
