namespace DataManager.Core.Models.Dacpac;

public class GenerationResult
{
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int EntitiesGenerated { get; set; }
    public int ViewsGenerated { get; set; }
    public int TablesSkipped { get; set; }
    public int ErrorsEncountered { get; set; }
    public List<ElementDiscoveryReport> DiscoveryReports { get; set; } = new();

    // DbSet usage replacement stats (populated when a mapping CSV is provided)
    public int DbSetReplacementsMapped { get; set; }
    public int DbSetReplacementsUnchanged { get; set; }
    public int DbSetReplacementFilesModified { get; set; }
    public List<string> DbSetUnmatchedOldNames { get; set; } = new();
}
