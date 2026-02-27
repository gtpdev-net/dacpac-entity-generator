namespace DataManager.Core.Models;

public class ExcelRow
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public bool TableInDaoAnalysis { get; set; }
    public string PersistenceType { get; set; } = string.Empty;
    public bool AddedByAPI { get; set; }
    public string DevPersistenceType { get; set; } = string.Empty;
    public bool Generate { get; set; }
}
