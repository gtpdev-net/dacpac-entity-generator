# Implementation Plan: Enhanced DACPAC Entity Generator

> **Implementation Status** (as of February 2026):
> 
> | Phase | Status |
> |-------|--------|
> | Phase 1 – New Data Models | ✅ Complete |
> | Phase 2 – Updated Existing Models | ✅ Complete |
> | Phase 3 – ModelXmlParserService Enhancements | ✅ Complete |
> | Phase 4 – EntityClassGenerator Enhancements | ✅ Complete |
> | Phase 5 – FileWriterService Enhancements | ✅ Complete |
> | Phase 6 – ReportWriterService (new) | ✅ Complete |
> | Phase 7 – DbContextGenerator (new) | ✅ Complete |
> | Phase 8 – Program.cs Orchestration | ✅ Complete |
> 
> All planned features are now implemented. Navigation properties (from foreign keys) were intentionally deferred — FK data is parsed and stored in the model but not yet emitted as C# navigation properties. Refer to [SPEC.md](SPEC.md) for the current authoritative technical reference.

## Overview

This plan adds support for HIGH priority database elements (foreign keys with navigation properties, auto-discovered views), MEDIUM priority features (check constraints, unique constraints, computed column persistence, row version tokens, enhanced indexes, user-defined functions), and a dual-format (JSON + HTML) discovery report for LOW priority items.

The implementation follows existing patterns in `ModelXmlParserService.cs` and `EntityClassGenerator.cs`, ensuring consistency with the current codebase architecture.

---

## Priority Classification

### HIGH Priority (Critical for Entity Relationships)
1. **Foreign Key Constraints** - Essential for entity relationships and navigation properties
2. **Views** - Common in enterprise databases, need keyless entity support

### MEDIUM Priority (Enhanced Database Fidelity)
3. **Check Constraints** - Data validation at database level
4. **Unique Constraints** - Alternate keys (distinct from unique indexes)
5. **Computed Column Persistence** - Already parsing computed columns, need persistence flag
6. **Row Version/Concurrency Tokens** - Optimistic concurrency support
7. **Enhanced Index Features** - Filtered indexes, included columns, sort order
8. **User-Defined Functions** - Reusable database logic

### LOW Priority (Documentation & Discovery)
9. **Extended Properties/Comments** - Column descriptions
10. **Column Collation** - Non-default collations
11. **Column Order** - Cosmetic ordering
12. **Sequences** - Alternative to identity columns
13. **Spatial Data Types** - Geography/Geometry support
14. **Hierarchyid** - Hierarchical data type

---

## Implementation Steps

### Phase 1: Data Models (New Classes)

#### Step 1.1: Create ForeignKeyDefinition.cs
**File**: `Models/ForeignKeyDefinition.cs`

```csharp
namespace DacpacEntityGenerator.Models;

public class ForeignKeyDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> FromColumns { get; set; } = new();
    public string ToSchema { get; set; } = string.Empty;
    public string ToTable { get; set; } = string.Empty;
    public List<string> ToColumns { get; set; } = new();
    public bool OnDeleteCascade { get; set; }
    public bool OnUpdateCascade { get; set; }
    public ForeignKeyCardinality Cardinality { get; set; }
}

public enum ForeignKeyCardinality
{
    Unknown,
    OneToMany,
    OneToOne,
    ManyToMany
}
```

#### Step 1.2: Create ViewDefinition.cs
**File**: `Models/ViewDefinition.cs`

```csharp
namespace DacpacEntityGenerator.Models;

public class ViewDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
    public List<ColumnDefinition> Columns { get; set; } = new();
    public bool HasStandardAuditColumns { get; set; }
}
```

#### Step 1.3: Create CheckConstraintDefinition.cs
**File**: `Models/CheckConstraintDefinition.cs`

```csharp
namespace DacpacEntityGenerator.Models;

public class CheckConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public List<string> AffectedColumns { get; set; } = new();
}
```

#### Step 1.4: Create UniqueConstraintDefinition.cs
**File**: `Models/UniqueConstraintDefinition.cs`

```csharp
namespace DacpacEntityGenerator.Models;

public class UniqueConstraintDefinition
{
    public string Name { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public bool IsClustered { get; set; }
}
```

#### Step 1.5: Create FunctionDefinition.cs
**File**: `Models/FunctionDefinition.cs`

```csharp
namespace DacpacEntityGenerator.Models;

public class FunctionDefinition
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public FunctionType Type { get; set; }
    public string ReturnType { get; set; } = string.Empty;
    public List<FunctionParameter> Parameters { get; set; } = new();
}

public enum FunctionType
{
    Scalar,
    TableValued,
    InlineTableValued
}

public class FunctionParameter
{
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = string.Empty;
    public bool IsOutput { get; set; }
}
```

#### Step 1.6: Create ElementDiscoveryReport.cs
**File**: `Models/ElementDiscoveryReport.cs`

```csharp
namespace DacpacEntityGenerator.Models;

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
```

---

### Phase 2: Update Existing Models

#### Step 2.1: Update TableDefinition.cs
**File**: `Models/TableDefinition.cs`

Add new properties:
```csharp
public List<ForeignKeyDefinition> ForeignKeys { get; set; } = new();
public List<CheckConstraintDefinition> CheckConstraints { get; set; } = new();
public List<UniqueConstraintDefinition> UniqueConstraints { get; set; } = new();
```

#### Step 2.2: Update ColumnDefinition.cs
**File**: `Models/ColumnDefinition.cs`

Add new properties:
```csharp
public bool IsComputed { get; set; }
public bool IsComputedPersisted { get; set; }
public string? ComputedExpression { get; set; }
public bool IsRowVersion { get; set; }
public bool IsConcurrencyToken { get; set; }
public string? Collation { get; set; }
public string? Description { get; set; }
```

#### Step 2.3: Update IndexDefinition.cs
**File**: `Models/IndexDefinition.cs`

Add new properties:
```csharp
public List<string> IncludedColumns { get; set; } = new();
public Dictionary<string, bool> ColumnSortOrder { get; set; } = new(); // true = ASC, false = DESC
public string? FilterDefinition { get; set; }
```

---

### Phase 3: ModelXmlParserService Enhancements

#### Step 3.1: Add ParseForeignKeys Method
**File**: `Services/ModelXmlParserService.cs`

Add after `ParseIndexes()` method (~line 710):

```csharp
private List<ForeignKeyDefinition> ParseForeignKeys(XDocument doc, string schema, string tableName)
{
    var foreignKeys = new List<ForeignKeyDefinition>();
    var fullTableName = $"[{schema}].[{tableName}]";

    // Find foreign key constraints
    var fkConstraints = doc.Descendants(_dacNamespace + "Element")
        .Where(e => e.Attribute("Type")?.Value == "SqlForeignKeyConstraint");

    foreach (var fkConstraint in fkConstraints)
    {
        // Check if this FK belongs to our table
        var relationshipToTable = fkConstraint.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

        if (relationshipToTable != null)
        {
            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef != null && tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
            {
                var fkName = fkConstraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";

                // Get properties
                var properties = fkConstraint.Elements(_dacNamespace + "Property")
                    .ToDictionary(
                        p => p.Attribute("Name")?.Value ?? "",
                        p => p.Attribute("Value")?.Value ?? "",
                        StringComparer.OrdinalIgnoreCase
                    );

                var deleteAction = properties.TryGetValue("DeleteAction", out var delAction) ? delAction : "NO_ACTION";
                var updateAction = properties.TryGetValue("UpdateAction", out var updAction) ? updAction : "NO_ACTION";

                // Get source columns
                var fromColumnsRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "Columns");
                var fromColumns = ExtractColumnNames(fromColumnsRelationship);

                // Get target table
                var toTableRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForeignTable");
                var toTableRef = toTableRelationship?.Elements(_dacNamespace + "Entry")
                    .Elements(_dacNamespace + "References")
                    .FirstOrDefault()?.Attribute("Name")?.Value;

                // Get target columns
                var toColumnsRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForeignColumns");
                var toColumns = ExtractColumnNames(toColumnsRelationship);

                if (!string.IsNullOrEmpty(toTableRef) && fromColumns.Any() && toColumns.Any())
                {
                    var toParts = toTableRef.Split('.');
                    var toSchema = toParts.Length > 1 ? toParts[0].Trim('[', ']') : "dbo";
                    var toTable = toParts.Last().Trim('[', ']');

                    foreignKeys.Add(new ForeignKeyDefinition
                    {
                        Name = fkName,
                        FromColumns = fromColumns,
                        ToSchema = toSchema,
                        ToTable = toTable,
                        ToColumns = toColumns,
                        OnDeleteCascade = deleteAction.Equals("CASCADE", StringComparison.OrdinalIgnoreCase),
                        OnUpdateCascade = updateAction.Equals("CASCADE", StringComparison.OrdinalIgnoreCase),
                        Cardinality = InferCardinality(fromColumns, toColumns)
                    });
                }
            }
        }
    }

    return foreignKeys;
}

private ForeignKeyCardinality InferCardinality(List<string> fromColumns, List<string> toColumns)
{
    // Simple heuristic: if single column, likely one-to-many
    if (fromColumns.Count == 1 && toColumns.Count == 1)
        return ForeignKeyCardinality.OneToMany;
    
    return ForeignKeyCardinality.Unknown;
}

private List<string> ExtractColumnNames(XElement? relationship)
{
    if (relationship == null) return new List<string>();

    return relationship.Elements(_dacNamespace + "Entry")
        .Elements(_dacNamespace + "References")
        .Select(r => r.Attribute("Name")?.Value)
        .Where(n => !string.IsNullOrEmpty(n))
        .Select(n => n!.Split('.').Last().Trim('[', ']'))
        .ToList();
}
```

#### Step 3.2: Add ParseViews Method
**File**: `Services/ModelXmlParserService.cs`

Add new public method:

```csharp
public List<ViewDefinition> ParseViews(string modelXml, string server, string database)
{
    try
    {
        var doc = XDocument.Parse(modelXml);
        var views = new List<ViewDefinition>();

        // Find all view elements
        var viewElements = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlView");

        foreach (var viewElement in viewElements)
        {
            var viewFullName = viewElement.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(viewFullName))
                continue;

            var parts = viewFullName.Split('.');
            if (parts.Length < 2)
                continue;

            var schema = parts[0].Trim('[', ']');
            var viewName = parts[1].Trim('[', ']');

            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{viewName}] - Parsing view");

            var viewDefinition = new ViewDefinition
            {
                Server = server,
                Database = database,
                Schema = schema,
                ViewName = viewName,
                Columns = ParseColumns(viewElement)
            };

            // Check if view has standard audit columns (Id, CreatedDate, ModifiedDate)
            viewDefinition.HasStandardAuditColumns = HasStandardAuditColumns(viewDefinition.Columns);

            if (viewDefinition.Columns.Count > 0)
            {
                views.Add(viewDefinition);
                ConsoleLogger.LogProgress($"[{server}].[{database}].[{schema}].[{viewName}] - View parsed with {viewDefinition.Columns.Count} columns");
            }
        }

        return views;
    }
    catch (Exception ex)
    {
        ConsoleLogger.LogError($"[{server}].[{database}] - Failed to parse views: {ex.Message}");
        return new List<ViewDefinition>();
    }
}

private bool HasStandardAuditColumns(List<ColumnDefinition> columns)
{
    var columnNames = columns.Select(c => c.Name.ToLowerInvariant()).ToHashSet();
    return columnNames.Contains("id") && 
           columnNames.Contains("createddate") && 
           columnNames.Contains("modifieddate");
}
```

#### Step 3.3: Add ParseCheckConstraints Method
**File**: `Services/ModelXmlParserService.cs`

```csharp
private List<CheckConstraintDefinition> ParseCheckConstraints(XDocument doc, string schema, string tableName)
{
    var checkConstraints = new List<CheckConstraintDefinition>();
    var fullTableName = $"[{schema}].[{tableName}]";

    // Find check constraint elements
    var constraints = doc.Descendants(_dacNamespace + "Element")
        .Where(e => e.Attribute("Type")?.Value == "SqlCheckConstraint");

    foreach (var constraint in constraints)
    {
        // Check if this constraint belongs to our table
        var relationshipToTable = constraint.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

        if (relationshipToTable != null)
        {
            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef != null && tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
            {
                var constraintName = constraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";

                // Get the check expression
                var expressionProperty = constraint.Elements(_dacNamespace + "Property")
                    .FirstOrDefault(p => p.Attribute("Name")?.Value == "ExpressionScript");

                if (expressionProperty != null)
                {
                    var valueElement = expressionProperty.Element(_dacNamespace + "Value");
                    if (valueElement != null)
                    {
                        var expression = valueElement.Value.Trim();
                        expression = CleanDefaultValueExpression(expression);

                        checkConstraints.Add(new CheckConstraintDefinition
                        {
                            Name = constraintName,
                            Expression = expression,
                            AffectedColumns = ExtractColumnNamesFromExpression(expression)
                        });
                    }
                }
            }
        }
    }

    return checkConstraints;
}

private List<string> ExtractColumnNamesFromExpression(string expression)
{
    // Simple pattern matching for [ColumnName] or ColumnName
    var matches = System.Text.RegularExpressions.Regex.Matches(expression, @"\[([^\]]+)\]|(\b[a-zA-Z_][a-zA-Z0-9_]*\b)");
    return matches
        .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
        .Where(s => !string.IsNullOrEmpty(s))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
```

#### Step 3.4: Add ParseUniqueConstraints Method
**File**: `Services/ModelXmlParserService.cs`

```csharp
private List<UniqueConstraintDefinition> ParseUniqueConstraints(XDocument doc, string schema, string tableName)
{
    var uniqueConstraints = new List<UniqueConstraintDefinition>();
    var fullTableName = $"[{schema}].[{tableName}]";

    // Find unique constraint elements (distinct from indexes)
    var constraints = doc.Descendants(_dacNamespace + "Element")
        .Where(e => e.Attribute("Type")?.Value == "SqlUniqueConstraint");

    foreach (var constraint in constraints)
    {
        // Check if this constraint belongs to our table
        var relationshipToTable = constraint.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

        if (relationshipToTable != null)
        {
            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef != null && tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
            {
                var constraintName = constraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";

                // Get properties
                var properties = constraint.Elements(_dacNamespace + "Property")
                    .ToDictionary(
                        p => p.Attribute("Name")?.Value ?? "",
                        p => p.Attribute("Value")?.Value ?? "",
                        StringComparer.OrdinalIgnoreCase
                    );

                var isClustered = properties.TryGetValue("IsClustered", out var clusteredVal) &&
                                  clusteredVal.Equals("True", StringComparison.OrdinalIgnoreCase);

                // Get columns
                var columnsRelationship = constraint.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "ColumnSpecifications");
                var columns = ExtractColumnNames(columnsRelationship);

                if (columns.Any())
                {
                    uniqueConstraints.Add(new UniqueConstraintDefinition
                    {
                        Name = constraintName,
                        Columns = columns,
                        IsClustered = isClustered
                    });
                }
            }
        }
    }

    return uniqueConstraints;
}
```

#### Step 3.5: Add ParseUserDefinedFunctions Method
**File**: `Services/ModelXmlParserService.cs`

```csharp
public List<FunctionDefinition> ParseUserDefinedFunctions(string modelXml, string server, string database)
{
    try
    {
        var doc = XDocument.Parse(modelXml);
        var functions = new List<FunctionDefinition>();

        // Find all function types
        var functionTypes = new[] { "SqlScalarFunction", "SqlTableValuedFunction", "SqlInlineTableValuedFunction" };
        
        foreach (var funcType in functionTypes)
        {
            var functionElements = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == funcType);

            foreach (var funcElement in functionElements)
            {
                var funcFullName = funcElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(funcFullName))
                    continue;

                var parts = funcFullName.Split('.');
                if (parts.Length < 2)
                    continue;

                var schema = parts[0].Trim('[', ']');
                var funcName = parts[1].Trim('[', ']');

                var funcDef = new FunctionDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    FunctionName = funcName,
                    Type = funcType switch
                    {
                        "SqlScalarFunction" => FunctionType.Scalar,
                        "SqlTableValuedFunction" => FunctionType.TableValued,
                        "SqlInlineTableValuedFunction" => FunctionType.InlineTableValued,
                        _ => FunctionType.Scalar
                    }
                };

                functions.Add(funcDef);
            }
        }

        return functions;
    }
    catch (Exception ex)
    {
        ConsoleLogger.LogError($"[{server}].[{database}] - Failed to parse functions: {ex.Message}");
        return new List<FunctionDefinition>();
    }
}
```

#### Step 3.6: Enhance ParseColumnProperties for Computed Columns
**File**: `Services/ModelXmlParserService.cs`

Update `ParseColumnProperties()` method to handle computed columns:

```csharp
// Inside ParseColumnProperties, add after existing property parsing:
if (columnElement.Attribute("Type")?.Value == "SqlComputedColumn")
{
    column.IsComputed = true;
    
    // Check if persisted
    if (properties.TryGetValue("IsPersisted", out var persisted))
    {
        column.IsComputedPersisted = persisted.Equals("True", StringComparison.OrdinalIgnoreCase);
    }
    
    // Get computed expression
    if (properties.TryGetValue("Expression", out var expr) || 
        properties.TryGetValue("ExpressionScript", out expr))
    {
        column.ComputedExpression = expr;
    }
}

// Check for rowversion/timestamp
if (column.SqlType.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
    column.SqlType.Equals("rowversion", StringComparison.OrdinalIgnoreCase))
{
    column.IsRowVersion = true;
    column.IsConcurrencyToken = true;
}

// Check for collation
if (properties.TryGetValue("Collation", out var collation))
{
    column.Collation = collation;
}
```

#### Step 3.7: Enhance ParseIndexes for Advanced Features
**File**: `Services/ModelXmlParserService.cs`

Update `ParseIndexes()` method around line 611:

```csharp
// Inside the foreach loop for column specifications, add:
var columnSpec = columnSpecElement.Elements(_dacNamespace + "Property")
    .ToDictionary(
        p => p.Attribute("Name")?.Value ?? "",
        p => p.Attribute("Value")?.Value ?? "",
        StringComparer.OrdinalIgnoreCase
    );

var isDescending = columnSpec.TryGetValue("IsDescending", out var descVal) &&
                   descVal.Equals("True", StringComparison.OrdinalIgnoreCase);

indexDef.ColumnSortOrder[columnName] = !isDescending; // true = ASC, false = DESC

// After parsing indexed columns, get included columns:
var includedColumnsRelationship = indexElement.Elements(_dacNamespace + "Relationship")
    .FirstOrDefault(r => r.Attribute("Name")?.Value == "IncludedColumns");

if (includedColumnsRelationship != null)
{
    indexDef.IncludedColumns = ExtractColumnNames(includedColumnsRelationship);
}

// Get filter predicate:
var filterProperty = indexElement.Elements(_dacNamespace + "Property")
    .FirstOrDefault(p => p.Attribute("Name")?.Value == "FilterPredicate");

if (filterProperty != null)
{
    var filterValue = filterProperty.Element(_dacNamespace + "Value");
    if (filterValue != null)
    {
        indexDef.FilterDefinition = filterValue.Value.Trim();
    }
}
```

#### Step 3.8: Add GenerateDiscoveryReport Method
**File**: `Services/ModelXmlParserService.cs`

```csharp
public ElementDiscoveryReport GenerateDiscoveryReport(string modelXml, string server, string database)
{
    try
    {
        var doc = XDocument.Parse(modelXml);
        var report = new ElementDiscoveryReport
        {
            Server = server,
            Database = database
        };

        // Count all element types
        var allElements = doc.Descendants(_dacNamespace + "Element")
            .Select(e => e.Attribute("Type")?.Value)
            .Where(t => !string.IsNullOrEmpty(t))
            .GroupBy(t => t!)
            .ToDictionary(g => g.Key, g => g.Count());

        report.ElementTypeCounts = allElements;

        // Extract sequences
        var sequences = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlSequence");

        foreach (var seq in sequences)
        {
            var seqName = seq.Attribute("Name")?.Value ?? "";
            report.Sequences.Add(new ElementDetail
            {
                Name = seqName,
                Location = $"[{server}].[{database}]",
                Type = "Sequence",
                Details = "Not automatically mapped to entities"
            });
        }

        // Extract stored procedures
        var storedProcs = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlProcedure");

        foreach (var sp in storedProcs)
        {
            var spName = sp.Attribute("Name")?.Value ?? "";
            report.StoredProcedures.Add(new ElementDetail
            {
                Name = spName,
                Location = $"[{server}].[{database}]",
                Type = "Stored Procedure",
                Details = "Call using FromSqlRaw() or FromSqlInterpolated()"
            });
        }

        // Extract triggers
        var triggers = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlDmlTrigger");

        foreach (var trigger in triggers)
        {
            var triggerName = trigger.Attribute("Name")?.Value ?? "";
            report.Triggers.Add(new ElementDetail
            {
                Name = triggerName,
                Location = $"[{server}].[{database}]",
                Type = "Trigger",
                Details = "Not configurable in EF Core"
            });
        }

        // Find spatial columns
        var spatialTables = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlSimpleColumn")
            .Where(col =>
            {
                var typeSpecifier = col.Descendants(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "TypeSpecifier");
                
                if (typeSpecifier != null)
                {
                    var props = typeSpecifier.Descendants(_dacNamespace + "Property")
                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "SqlDataType");
                    
                    var sqlType = props?.Attribute("Value")?.Value;
                    return sqlType?.Equals("Geography", StringComparison.OrdinalIgnoreCase) == true ||
                           sqlType?.Equals("Geometry", StringComparison.OrdinalIgnoreCase) == true;
                }
                return false;
            });

        foreach (var col in spatialTables)
        {
            var colName = col.Attribute("Name")?.Value ?? "";
            report.SpatialColumns.Add(new ElementDetail
            {
                Name = colName,
                Location = $"[{server}].[{database}]",
                Type = "Spatial Column",
                Details = "Requires NetTopologySuite package"
            });
        }

        // Find hierarchyid columns
        var hierarchyIdColumns = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlSimpleColumn")
            .Where(col =>
            {
                var typeSpecifier = col.Descendants(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "TypeSpecifier");
                
                if (typeSpecifier != null)
                {
                    var props = typeSpecifier.Descendants(_dacNamespace + "Property")
                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "SqlDataType");
                    
                    return props?.Attribute("Value")?.Value?.Equals("Hierarchyid", StringComparison.OrdinalIgnoreCase) == true;
                }
                return false;
            });

        foreach (var col in hierarchyIdColumns)
        {
            var colName = col.Attribute("Name")?.Value ?? "";
            report.HierarchyIdColumns.Add(new ElementDetail
            {
                Name = colName,
                Location = $"[{server}].[{database}]",
                Type = "HierarchyId Column",
                Details = "Requires custom value converter"
            });
        }

        // Identify unhandled element types
        var handledTypes = new HashSet<string>
        {
            "SqlTable", "SqlSimpleColumn", "SqlComputedColumn",
            "SqlPrimaryKeyConstraint", "SqlIndex", "SqlDefaultConstraint",
            "SqlForeignKeyConstraint", "SqlCheckConstraint", "SqlUniqueConstraint",
            "SqlView", "SqlScalarFunction", "SqlTableValuedFunction", "SqlInlineTableValuedFunction"
        };

        report.UnhandledElementTypes = allElements.Keys
            .Where(t => !handledTypes.Contains(t))
            .OrderBy(t => t)
            .ToList();

        ConsoleLogger.LogInfo($"[{server}].[{database}] - Discovery report generated: {report.Sequences.Count} sequences, {report.StoredProcedures.Count} stored procedures, {report.Triggers.Count} triggers");

        return report;
    }
    catch (Exception ex)
    {
        ConsoleLogger.LogError($"[{server}].[{database}] - Failed to generate discovery report: {ex.Message}");
        return new ElementDiscoveryReport { Server = server, Database = database };
    }
}
```

#### Step 3.9: Update ParseTable Orchestration
**File**: `Services/ModelXmlParserService.cs`

Update the `ParseTable()` method around line 102:

```csharp
// After line 102 (index parsing):
tableDefinition.Indexes = ParseIndexes(doc, schema, tableName);
ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.Indexes.Count} existing indexes");

// ADD THESE LINES:
// Parse foreign keys
tableDefinition.ForeignKeys = ParseForeignKeys(doc, schema, tableName);
ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.ForeignKeys.Count} foreign keys");

// Parse check constraints
tableDefinition.CheckConstraints = ParseCheckConstraints(doc, schema, tableName);
ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.CheckConstraints.Count} check constraints");

// Parse unique constraints
tableDefinition.UniqueConstraints = ParseUniqueConstraints(doc, schema, tableName);
ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.UniqueConstraints.Count} unique constraints");

// Continue with existing PK index logic...
```

---

### Phase 4: EntityClassGenerator Enhancements

#### Step 4.1: Add Navigation Properties to Entity Generation
**File**: `Services/EntityClassGenerator.cs`

Update `GenerateEntityClass()` around line 71 (after regular properties):

```csharp
// After generating regular properties, add navigation properties
if (table.ForeignKeys.Count > 0)
{
    sb.AppendLine();
    sb.AppendLine("        // Navigation Properties");
    
    foreach (var fk in table.ForeignKeys)
    {
        var navPropertyName = NameConverter.ToPascalCase(fk.ToTable);
        var navPropertyType = $"Core.Entities.{table.Server}.{table.Database}.{navPropertyName}";
        
        // Add nullable navigation property
        sb.AppendLine($"        public virtual {navPropertyType}? {navPropertyName} {{ get; set; }}");
    }
}
```

#### Step 4.2: Enhance Property Attribute Generation
**File**: `Services/EntityClassGenerator.cs`

Update `GenerateProperty()` method around line 82:

```csharp
// Add these attributes before existing [Column] attribute:

// Row version / concurrency token
if (column.IsRowVersion || column.IsConcurrencyToken)
{
    sb.AppendLine($"        [Timestamp]");
}

// Computed column
if (column.IsComputed)
{
    if (column.IsComputedPersisted)
    {
        sb.AppendLine($"        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]");
    }
    else
    {
        sb.AppendLine($"        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]");
    }
}

// Continue with existing [Column] attribute...
```

#### Step 4.3: Enhance Configuration Generation
**File**: `Services/EntityClassGenerator.cs`

Update `GenerateOnModelCreatingBody()` around line 225:

```csharp
// After index configurations, add foreign key configurations:
foreach (var fk in table.ForeignKeys)
{
    var fromProps = string.Join(", ", fk.FromColumns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
    var toEntity = $"Core.Entities.{NameConverter.ToPascalCase(table.Server)}.{NameConverter.ToPascalCase(table.Database)}.{NameConverter.ToPascalCase(fk.ToTable)}";
    
    sb.Append($"            modelBuilder.Entity<{fqn}>().HasOne<{toEntity}>()");
    sb.Append(".WithMany()");
    
    if (fk.FromColumns.Count == 1)
    {
        sb.Append($".HasForeignKey(e => e.{NameConverter.ToPascalCase(fk.FromColumns[0])})");
    }
    else
    {
        sb.Append($".HasForeignKey(e => new {{ {fromProps} }})");
    }
    
    if (fk.OnDeleteCascade)
    {
        sb.Append(".OnDelete(DeleteBehavior.Cascade)");
    }
    
    sb.AppendLine($".HasConstraintName(\"{fk.Name}\");");
}

// Add check constraints:
foreach (var check in table.CheckConstraints)
{
    var escapedExpr = check.Expression.Replace("\"", "\\\"");
    sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasCheckConstraint(\"{check.Name}\", \"{escapedExpr}\");");
}

// Add unique constraints (alternate keys):
foreach (var unique in table.UniqueConstraints)
{
    if (unique.Columns.Count == 1)
    {
        var colName = NameConverter.ToPascalCase(unique.Columns[0]);
        sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasAlternateKey(e => e.{colName}).HasName(\"{unique.Name}\");");
    }
    else
    {
        var cols = string.Join(", ", unique.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
        sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasAlternateKey(e => new {{ {cols} }}).HasName(\"{unique.Name}\");");
    }
}

// Enhance existing property configurations:
foreach (var column in table.Columns)
{
    var propertyName = NameConverter.ToPascalCase(column.Name);
    var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out _);
    
    var configurations = new List<string>();
    
    // Existing decimal configuration...
    
    // ADD: Collation
    if (!string.IsNullOrEmpty(column.Collation))
    {
        configurations.Add($"UseCollation(\"{column.Collation}\")");
    }
    
    // ADD: Concurrency token
    if (column.IsConcurrencyToken && !column.IsRowVersion)
    {
        configurations.Add("IsConcurrencyToken()");
    }
    
    // Existing default value configuration...
}

// Enhance existing index configuration with filter:
if (!string.IsNullOrEmpty(index.FilterDefinition))
{
    var escapedFilter = index.FilterDefinition.Replace("\"", "\\\"");
    indexConfig += $".HasFilter(\"{escapedFilter}\")";
}

// Add comment if index has features not supported by HasIndex:
if (index.IncludedColumns.Any() || index.ColumnSortOrder.Any(kvp => !kvp.Value))
{
    sb.AppendLine($"            // Note: Index '{index.Name}' has included columns or DESC sort order - configure in migrations");
}
```

#### Step 4.4: Add View Class Generation
**File**: `Services/EntityClassGenerator.cs`

Add new method:

```csharp
public string GenerateViewClass(ViewDefinition view)
{
    var sb = new StringBuilder();

    // Using statements
    sb.AppendLine("using System;");
    sb.AppendLine("using System.ComponentModel.DataAnnotations;");
    sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
    
    if (view.HasStandardAuditColumns)
    {
        sb.AppendLine("using DataLayer.Core.Entities;");
    }
    
    sb.AppendLine();

    // Namespace
    var namespaceName = $"DataLayer.Core.Views.{view.Server}.{view.Database}";
    sb.AppendLine($"namespace {namespaceName}");
    sb.AppendLine("{");

    // Class header with comment
    sb.AppendLine($"    /// <summary>");
    sb.AppendLine($"    /// View: [{view.Schema}].[{view.ViewName}]");
    sb.AppendLine($"    /// Source: [{view.Server}].[{view.Database}]");
    sb.AppendLine($"    /// </summary>");
    sb.AppendLine($"    [Keyless]");
    
    var className = NameConverter.ToPascalCase(view.ViewName);
    
    if (view.HasStandardAuditColumns)
    {
        sb.AppendLine($"    public class {className} : BaseEntity");
    }
    else
    {
        sb.AppendLine($"    public class {className}");
    }
    
    sb.AppendLine("    {");

    // Generate properties (all init-only for read-only views)
    foreach (var column in view.Columns)
    {
        GenerateViewProperty(sb, column);
    }

    sb.AppendLine("    }");
    sb.AppendLine("}");

    return sb.ToString();
}

private void GenerateViewProperty(StringBuilder sb, ColumnDefinition column)
{
    var propertyName = NameConverter.ToPascalCase(column.Name);
    var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out var needsMaxLength);

    // [Column] attribute
    sb.AppendLine($"        [Column(\"{column.Name}\")]");

    // [MaxLength] for strings
    if (needsMaxLength && column.MaxLength.HasValue && column.MaxLength.Value > 0)
    {
        sb.AppendLine($"        [MaxLength({column.MaxLength.Value})]");
    }

    // Property declaration - init-only
    if (column.IsNullable || csharpType.EndsWith("?"))
    {
        sb.AppendLine($"        public {csharpType} {propertyName} {{ get; init; }}");
    }
    else
    {
        sb.AppendLine($"        public required {csharpType} {propertyName} {{ get; init; }}");
    }
    
    sb.AppendLine();
}

public string GenerateViewConfiguration(List<ViewDefinition> views, string server, string database)
{
    var sb = new StringBuilder();
    
    foreach (var view in views)
    {
        var className = NameConverter.ToPascalCase(view.ViewName);
        var fqn = $"Core.Views.{server}.{database}.{className}";
        
        sb.Append($"            modelBuilder.Entity<{fqn}>()");
        sb.Append($".HasNoKey()");
        sb.AppendLine($".ToView(\"{view.ViewName}\", \"{view.Schema}\");");
    }
    
    return sb.ToString();
}
```

---

### Phase 5: Reporting Service

#### Step 5.1: Create ReportWriterService.cs
**File**: `Services/ReportWriterService.cs`

```csharp
using System.Text;
using System.Text.Json;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class ReportWriterService
{
    public bool WriteJsonReport(string outputDirectory, List<ElementDiscoveryReport> reports)
    {
        try
        {
            var reportDir = Path.Combine(outputDirectory, "Reports");
            Directory.CreateDirectory(reportDir);
            
            var reportPath = Path.Combine(reportDir, "DiscoveryReport.json");
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(reports, options);
            File.WriteAllText(reportPath, json, Encoding.UTF8);
            
            ConsoleLogger.LogProgress($"JSON discovery report written to: Reports/DiscoveryReport.json");
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Failed to write JSON report: {ex.Message}");
            return false;
        }
    }

    public bool WriteHtmlReport(string outputDirectory, List<ElementDiscoveryReport> reports)
    {
        try
        {
            var reportDir = Path.Combine(outputDirectory, "Reports");
            Directory.CreateDirectory(reportDir);
            
            var reportPath = Path.Combine(reportDir, "DiscoveryReport.html");
            var html = GenerateHtmlReport(reports);
            File.WriteAllText(reportPath, html, Encoding.UTF8);
            
            ConsoleLogger.LogProgress($"HTML discovery report written to: Reports/DiscoveryReport.html");
            return true;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Failed to write HTML report: {ex.Message}");
            return false;
        }
    }

    private string GenerateHtmlReport(List<ElementDiscoveryReport> reports)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>DACPAC Discovery Report</title>");
        sb.AppendLine("    <link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css\" rel=\"stylesheet\">");
        sb.AppendLine("    <style>");
        sb.AppendLine("        .summary-card { margin-bottom: 1rem; }");
        sb.AppendLine("        .element-section { margin-top: 2rem; }");
        sb.AppendLine("        .badge-custom { font-size: 0.9rem; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container my-5\">");
        sb.AppendLine("        <h1 class=\"mb-4\">DACPAC Element Discovery Report</h1>");
        sb.AppendLine($"        <p class=\"text-muted\">Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        
        // Summary cards
        sb.AppendLine("        <div class=\"row\">");
        AddSummaryCard(sb, "Databases", reports.Count.ToString(), "bg-primary");
        AddSummaryCard(sb, "Sequences", reports.Sum(r => r.Sequences.Count).ToString(), "bg-info");
        AddSummaryCard(sb, "Stored Procedures", reports.Sum(r => r.StoredProcedures.Count).ToString(), "bg-warning");
        AddSummaryCard(sb, "Triggers", reports.Sum(r => r.Triggers.Count).ToString(), "bg-danger");
        sb.AppendLine("        </div>");
        
        // Database sections
        foreach (var report in reports)
        {
            sb.AppendLine($"        <div class=\"card mt-4\">");
            sb.AppendLine($"            <div class=\"card-header\">");
            sb.AppendLine($"                <h3>[{report.Server}].[{report.Database}]</h3>");
            sb.AppendLine($"            </div>");
            sb.AppendLine($"            <div class=\"card-body\">");
            
            AddElementTable(sb, "Sequences", report.Sequences);
            AddElementTable(sb, "Stored Procedures", report.StoredProcedures);
            AddElementTable(sb, "Triggers", report.Triggers);
            AddElementTable(sb, "Spatial Columns", report.SpatialColumns);
            AddElementTable(sb, "HierarchyId Columns", report.HierarchyIdColumns);
            
            if (report.UnhandledElementTypes.Any())
            {
                sb.AppendLine("                <h5 class=\"mt-3\">Unhandled Element Types</h5>");
                sb.AppendLine("                <div class=\"alert alert-info\">");
                foreach (var elementType in report.UnhandledElementTypes)
                {
                    sb.AppendLine($"                    <span class=\"badge bg-secondary me-2\">{elementType}</span>");
                }
                sb.AppendLine("                </div>");
            }
            
            sb.AppendLine($"            </div>");
            sb.AppendLine($"        </div>");
        }
        
        sb.AppendLine("    </div>");
        sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js\"></script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        
        return sb.ToString();
    }

    private void AddSummaryCard(StringBuilder sb, string title, string value, string bgClass)
    {
        sb.AppendLine($"            <div class=\"col-md-3\">");
        sb.AppendLine($"                <div class=\"card text-white {bgClass} summary-card\">");
        sb.AppendLine($"                    <div class=\"card-body\">");
        sb.AppendLine($"                        <h5 class=\"card-title\">{title}</h5>");
        sb.AppendLine($"                        <p class=\"card-text display-4\">{value}</p>");
        sb.AppendLine($"                    </div>");
        sb.AppendLine($"                </div>");
        sb.AppendLine($"            </div>");
    }

    private void AddElementTable(StringBuilder sb, string title, List<ElementDetail> elements)
    {
        if (!elements.Any())
            return;
        
        sb.AppendLine($"                <h5 class=\"mt-3\">{title} ({elements.Count})</h5>");
        sb.AppendLine($"                <table class=\"table table-striped table-sm\">");
        sb.AppendLine($"                    <thead>");
        sb.AppendLine($"                        <tr>");
        sb.AppendLine($"                            <th>Name</th>");
        sb.AppendLine($"                            <th>Location</th>");
        sb.AppendLine($"                            <th>Details</th>");
        sb.AppendLine($"                        </tr>");
        sb.AppendLine($"                    </thead>");
        sb.AppendLine($"                    <tbody>");
        
        foreach (var element in elements)
        {
            sb.AppendLine($"                        <tr>");
            sb.AppendLine($"                            <td><code>{element.Name}</code></td>");
            sb.AppendLine($"                            <td>{element.Location}</td>");
            sb.AppendLine($"                            <td>{element.Details}</td>");
            sb.AppendLine($"                        </tr>");
        }
        
        sb.AppendLine($"                    </tbody>");
        sb.AppendLine($"                </table>");
    }

    public Dictionary<string, int> AggregateReports(List<ElementDiscoveryReport> reports)
    {
        var aggregated = new Dictionary<string, int>
        {
            ["Total Databases"] = reports.Count,
            ["Total Sequences"] = reports.Sum(r => r.Sequences.Count),
            ["Total Stored Procedures"] = reports.Sum(r => r.StoredProcedures.Count),
            ["Total Triggers"] = reports.Sum(r => r.Triggers.Count),
            ["Total Spatial Columns"] = reports.Sum(r => r.SpatialColumns.Count),
            ["Total HierarchyId Columns"] = reports.Sum(r => r.HierarchyIdColumns.Count)
        };
        
        return aggregated;
    }
}
```

---

### Phase 6: Program.cs Integration

#### Step 6.1: Initialize Services
**File**: `Program.cs`

Add after existing service initialization (around line 42):

```csharp
var reportWriter = new ReportWriterService();
var allDiscoveryReports = new List<ElementDiscoveryReport>();
var allViews = new List<ViewDefinition>();
```

#### Step 6.2: Generate Discovery Reports
**File**: `Program.cs`

Add after line 91 (DACPAC extraction, before table processing):

```csharp
// Generate discovery report
var discoveryReport = modelXmlParser.GenerateDiscoveryReport(modelXml, server, database);
allDiscoveryReports.Add(discoveryReport);

ConsoleLogger.LogInfo($"[{server}].[{database}] - Discovery: {discoveryReport.StoredProcedures.Count} stored procedures, {discoveryReport.Sequences.Count} sequences");
```

#### Step 6.3: Process Views
**File**: `Program.cs`

Add after table processing loop (around line 168):

```csharp
// Parse and generate view entities
ConsoleLogger.LogInfo($"[{server}].[{database}] - Parsing views from DACPAC");
var views = modelXmlParser.ParseViews(modelXml, server, database);

foreach (var view in views)
{
    var viewCode = entityGenerator.GenerateViewClass(view);
    
    if (fileWriter.WriteViewFile(outputDirectory, server, database, view.Schema, view.ViewName, viewCode))
    {
        result.ViewsGenerated++;
        allViews.Add(view);
    }
}

ConsoleLogger.LogProgress($"[{server}].[{database}] - Generated {views.Count} view entities");
```

#### Step 6.4: Add View Configuration to OnModelCreating
**File**: `Program.cs`

Update configuration generation (around line 180):

```csharp
// Group views by server/database
var viewsByServerDatabase = allViews
    .GroupBy(v => new { v.Server, v.Database })
    .ToDictionary(g => g.Key, g => g.ToList());

foreach (var group in tablesByServerDatabase)
{
    var server = group.Key.Server;
    var database = group.Key.Database;
    var tables = group.Value;
    
    // Get views for this database
    var views = viewsByServerDatabase.TryGetValue(new { Server = server, Database = database }, out var v) ? v : new List<ViewDefinition>();

    var configurationCode = entityGenerator.GenerateEntityConfiguration(server, database, tables);
    
    // Add view configuration
    if (views.Any())
    {
        configurationCode += "\n\n            // View configurations\n";
        configurationCode += entityGenerator.GenerateViewConfiguration(views, server, database);
    }
    
    fileWriter.WriteConfigurationFile(outputDirectory, server, database, configurationCode);
}
```

#### Step 6.5: Write Discovery Reports
**File**: `Program.cs`

Add at the end before result summary (around line 185):

```csharp
// Write discovery reports
if (allDiscoveryReports.Any())
{
    ConsoleLogger.LogInfo("Generating discovery reports...");
    
    var aggregated = reportWriter.AggregateReports(allDiscoveryReports);
    foreach (var kvp in aggregated)
    {
        ConsoleLogger.LogInfo($"  {kvp.Key}: {kvp.Value}");
    }
    
    reportWriter.WriteJsonReport(outputDirectory, allDiscoveryReports);
    reportWriter.WriteHtmlReport(outputDirectory, allDiscoveryReports);
}
```

#### Step 6.6: Update GenerationResult Model
**File**: `Models/GenerationResult.cs`

Add new property:

```csharp
public int ViewsGenerated { get; set; }
```

Update final result logging:

```csharp
ConsoleLogger.LogProgress($"Generation complete: {result.EntitiesGenerated} entities, {result.ViewsGenerated} views, {result.TablesSkipped} skipped, {result.ErrorsEncountered} errors");
```

---

### Phase 7: FileWriterService Enhancement

#### Step 7.1: Add WriteViewFile Method
**File**: `Services/FileWriterService.cs`

```csharp
public bool WriteViewFile(string outputDirectory, string server, string database, string schema, string viewName, string viewClassCode)
{
    try
    {
        var viewsDir = Path.Combine(outputDirectory, "Views");
        var serverDir = Path.Combine(viewsDir, server);
        var databaseDir = Path.Combine(serverDir, database);
        
        Directory.CreateDirectory(databaseDir);

        var className = NameConverter.ToPascalCase(viewName);
        var fileName = $"{className}.cs";
        var filePath = Path.Combine(databaseDir, fileName);

        File.WriteAllText(filePath, viewClassCode, Encoding.UTF8);

        var relativePath = Path.GetRelativePath(outputDirectory, filePath);
        ConsoleLogger.LogProgress($"[{server}].[{database}].[{schema}].[{viewName}] - View entity written to: {relativePath}");

        return true;
    }
    catch (Exception ex)
    {
        ConsoleLogger.LogError($"[{server}].[{database}].[{schema}].[{viewName}] - Failed to write view file: {ex.Message}");
        return false;
    }
}
```

---

### Phase 8: Documentation Updates

#### Step 8.1: Update SPEC.md
**File**: `SPEC.md`

Add sections for:
1. New models in "Data Models" section
2. New parsing methods in "Services" section
3. Enhanced FluentAPI examples showing FKs, check constraints, etc.
4. Discovery report structure and output format

#### Step 8.2: Update README.md
**File**: `README.md`

Document:
1. New foreign key relationship support with navigation properties
2. Auto-discovered view entity generation
3. Enhanced constraint support
4. Discovery report location and format

---

## Testing Strategy

### Test Data Requirements
1. DACPAC with foreign keys (one-to-many, one-to-one relationships)
2. DACPAC with views (with and without standard audit columns)
3. DACPAC with check constraints
4. DACPAC with unique constraints (not indexes)
5. DACPAC with computed columns (persisted and non-persisted)
6. DACPAC with filtered indexes
7. DACPAC with rowversion/timestamp columns
8. DACPAC with sequences, stored procedures, triggers (for discovery report)

### Verification Steps
1. **Entity Generation**: Verify navigation properties present, correct types
2. **View Generation**: Verify `[Keyless]` attribute, init-only properties
3. **Configuration**: Verify `HasForeignKey()`, `OnDelete()`, `HasCheckConstraint()`, `HasAlternateKey()` present
4. **Discovery Reports**: Verify JSON structure, HTML renders correctly in browser
5. **EF Core Compatibility**: Create test DbContext, apply migrations, verify no errors
6. **LINQ Support**: Test navigation property traversal (`user.Orders.Where(...)`)

---

## Rollout Plan

### Phase 1 (Week 1): Core Models & Foreign Keys
- Implement all model classes
- Implement `ParseForeignKeys()`
- Update `TableDefinition`
- Generate FK configurations
- Test with simple FK relationships

### Phase 2 (Week 2): Views & Enhanced Constraints
- Implement `ParseViews()`
- Implement view class generation
- Implement check constraints parsing
- Implement unique constraints parsing
- Test with real database views

### Phase 3 (Week 3): Enhanced Index Features & Column Properties
- Enhance `ParseIndexes()` for filtered indexes, included columns
- Enhance column parsing for computed, rowversion, collation
- Update property configuration generation
- Test with complex indexes

### Phase 4 (Week 4): Discovery Reports & Documentation
- Implement `GenerateDiscoveryReport()`
- Implement `ReportWriterService`
- Generate JSON and HTML reports
- Update SPEC.md and README.md
- Comprehensive testing

---

## Success Criteria

1. ✅ Foreign keys parsed from DACPAC and navigation properties generated
2. ✅ Views auto-discovered and keyless entities generated
3. ✅ Check constraints, unique constraints, enhanced indexes parsed and configured
4. ✅ Discovery report (JSON + HTML) lists all LOW priority items with locations
5. ✅ Generated entities compile without errors
6. ✅ Generated configurations work in EF Core without manual modifications
7. ✅ All tests pass with sample DACPAC files
8. ✅ Documentation updated and complete

---

## Design Decisions

### Navigation Properties
**Decision**: Generate virtual navigation properties for all foreign keys
**Rationale**: Enables LINQ navigation (`user.Orders`), improves developer experience
**Trade-off**: Adds complexity to entity classes

### View Discovery
**Decision**: Auto-discover all views (not Excel-driven)
**Rationale**: Views don't participate in column selection like tables; simpler to discover all
**Trade-off**: May generate unwanted view entities (can be manually deleted)

### Report Format
**Decision**: Both JSON and HTML reports
**Rationale**: JSON for tooling integration, HTML for human review
**Trade-off**: Slight overhead generating both formats

### View Entity Pattern
**Decision**: Views extend `BaseEntity` only if they have standard audit columns
**Rationale**: Most views don't have Id/audit columns; `[Keyless]` is more appropriate
**Trade-off**: Inconsistent base class (some views have BaseEntity, some don't)

### Included Columns/Sort Order
**Decision**: Generate as comments (not in HasIndex configuration)
**Rationale**: EF Core's `HasIndex()` doesn't support these SQL Server features
**Trade-off**: Developers must create manual migrations for these features

---

## Risk Mitigation

### Risk: Breaking existing functionality
**Mitigation**: All new methods are additive; existing code unchanged except orchestration

### Risk: DACPAC format variations
**Mitigation**: Extensive null checking, try-catch blocks, fallback to existing behavior

### Risk: Performance with large DACPACs
**Mitigation**: Discovery report generation is optional; parsing is on-demand per table

### Risk: Invalid EF Core configurations
**Mitigation**: Follow official EF Core FluentAPI patterns; validate with test DbContext

---

## Future Enhancements (Out of Scope)

1. Many-to-many relationship detection (requires junction table analysis)
2. Inheritance mapping (TPH, TPT, TPC)
3. Custom value converters for spatial types
4. Stored procedure parameter extraction and method generation
5. Migration script generation
6. Reverse navigation properties (collection properties on "one" side)
