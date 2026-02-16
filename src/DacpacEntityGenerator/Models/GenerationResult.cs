namespace DacpacEntityGenerator.Models;

public class GenerationResult
{
    public bool Success { get; set; }
    public List<string> Messages { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int EntitiesGenerated { get; set; }
    public int ViewsGenerated { get; set; }
    public int TablesSkipped { get; set; }
    public int ErrorsEncountered { get; set; }
}
