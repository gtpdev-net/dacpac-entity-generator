namespace Catalogue.Web.Services;

public class BreadcrumbItem
{
    public string Label { get; init; } = string.Empty;
    public string? Href { get; init; }
    public bool IsActive { get; init; }

    public BreadcrumbItem() { }
    public BreadcrumbItem(string label, string? href = null, bool isActive = false)
    {
        Label = label;
        Href = href;
        IsActive = isActive;
    }
}
