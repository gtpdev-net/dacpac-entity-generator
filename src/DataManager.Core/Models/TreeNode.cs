namespace DataManager.Core.Models;

public class TreeNode
{
    public string Id { get; set; } = string.Empty;          // Composite key e.g. "server-1/db-3/schema-dbo/table-12"
    public string Label { get; set; } = string.Empty;
    public TreeNodeType NodeType { get; set; }
    public int? EntityId { get; set; }                       // PK of underlying entity (null for category/virtual nodes)
    public string? ParentId { get; set; }
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool IsLoading { get; set; }
    public bool ChildrenLoaded { get; set; }
    public int? ChildCount { get; set; }
    public string? BadgeText { get; set; }
    public bool? IsActive { get; set; }
    public List<TreeNode> Children { get; set; } = new();
}

public enum TreeNodeType
{
    ServerCategory,
    Server,
    DatabaseCategory,
    Database,
    SchemaCategory,
    Schema,
    TableCategory,
    Table,
    ColumnCategory,
    Column,
    IndexCategory,
    Index,
    ForeignKeyCategory,
    ForeignKey,
    CheckConstraintCategory,
    CheckConstraint,
    UniqueConstraintCategory,
    UniqueConstraint,
    TriggerCategory,
    Trigger,
    ViewCategory,
    View,
    ViewColumn,
    StoredProcedureCategory,
    StoredProcedure,
    StoredProcedureParameter,
    FunctionCategory,
    Function
}
