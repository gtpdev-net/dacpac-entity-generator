namespace DacpacEntityGenerator.Core.Models;

public class ElementDiscoveryReport
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;

    // LOW Priority Items
    public List<ElementDetail> ExtendedProperties { get; set; } = new();
    public List<ElementDetail> NonDefaultCollations { get; set; } = new();
    public List<ElementDetail> Sequences { get; set; } = new();
    public List<ElementDetail> SpatialColumns { get; set; } = new();
    public List<ElementDetail> HierarchyIdColumns { get; set; } = new();
    public List<ElementDetail> StoredProcedures { get; set; } = new();
    public List<ElementDetail> Triggers { get; set; } = new();

    // Summary counts
    public Dictionary<string, int> ElementTypeCounts { get; set; } = new();
    public List<string> UnhandledElementTypes { get; set; } = new();
}

public class ElementDetail
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
