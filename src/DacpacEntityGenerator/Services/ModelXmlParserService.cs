using System.Xml.Linq;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class ModelXmlParserService
{
    private XNamespace _dacNamespace = "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02";

    public TableDefinition? ParseTable(
        string modelXml,
        string server,
        string database,
        string schema,
        string tableName,
        List<string> requiredColumns)
    {
        try
        {
            var doc = XDocument.Parse(modelXml);

            // Find the table element
            var tableElement = FindTableElement(doc, schema, tableName);
            if (tableElement == null)
            {
                ConsoleLogger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Table not found in DACPAC");
                return null;
            }

            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Parsing table");

            var tableDefinition = new TableDefinition
            {
                Server = server,
                Database = database,
                Schema = schema,
                TableName = tableName,
                Columns = new List<ColumnDefinition>()
            };

            // Get all columns from the table
            var allColumns = ParseColumns(tableElement);

            // Get primary key columns
            var primaryKeyColumns = ParsePrimaryKey(doc, schema, tableName);

            // Process columns: include those in Excel filter plus all PK columns
            foreach (var column in allColumns)
            {
                var isPrimaryKey = primaryKeyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                var isRequiredByExcel = requiredColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);

                // Include if it's required by Excel OR if it's a primary key
                if (isRequiredByExcel || isPrimaryKey)
                {
                    column.IsPrimaryKey = isPrimaryKey;
                    column.IsFromExcel = isRequiredByExcel;
                    tableDefinition.Columns.Add(column);
                }
            }

            // Log warnings for columns in Excel but not found in DACPAC
            foreach (var requiredCol in requiredColumns)
            {
                if (!allColumns.Any(c => c.Name.Equals(requiredCol, StringComparison.OrdinalIgnoreCase)))
                {
                    ConsoleLogger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Column from Excel not found in table: {requiredCol}");
                }
            }

            if (tableDefinition.Columns.Count == 0)
            {
                ConsoleLogger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Table has no columns after filtering - skipping");
                return null;
            }

            if (!primaryKeyColumns.Any())
            {
                ConsoleLogger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Table has no primary key");
            }

            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Table has {tableDefinition.Columns.Count} columns ({tableDefinition.Columns.Count(c => c.IsPrimaryKey)} PK, {tableDefinition.Columns.Count(c => c.IsFromExcel)} from Excel)");

            return tableDefinition;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"[{server}].[{database}].[{schema}].[{tableName}] - Failed to parse table: {ex.Message}");
            return null;
        }
    }

    private XElement? FindTableElement(XDocument doc, string schema, string tableName)
    {
        // Table names in model.xml are typically formatted as [schema].[tablename]
        var fullTableName = $"[{schema}].[{tableName}]";

        var tables = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlTable");

        foreach (var table in tables)
        {
            var nameAttr = table.Attribute("Name")?.Value;
            if (nameAttr != null && nameAttr.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
            {
                return table;
            }
        }

        return null;
    }

    private List<ColumnDefinition> ParseColumns(XElement tableElement)
    {
        var columns = new List<ColumnDefinition>();

        var relationshipElements = tableElement.Elements(_dacNamespace + "Relationship")
            .Where(r => r.Attribute("Name")?.Value == "Columns");

        foreach (var relationship in relationshipElements)
        {
            var entryElements = relationship.Elements(_dacNamespace + "Entry");
            foreach (var entry in entryElements)
            {
                // SQL Server 2017 DACPAC: columns are Elements of type SqlSimpleColumn inside Entry
                var columnElement = entry.Element(_dacNamespace + "Element");
                if (columnElement != null && columnElement.Attribute("Type")?.Value == "SqlSimpleColumn")
                {
                    var columnName = columnElement.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(columnName))
                        continue;

                    var parts = columnName.Split('.');
                    var cleanColumnName = parts.Last().Trim('[', ']');
                    var column = ParseColumnProperties(columnElement, cleanColumnName);
                    columns.Add(column);
                    continue;
                }

                // Fallback: original pattern using References
                var references = entry.Elements(_dacNamespace + "References")
                    .Where(r => r.Attribute("Name")?.Value != null);

                foreach (var reference in references)
                {
                    var columnName = reference.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(columnName))
                        continue;

                    var parts = columnName.Split('.');
                    var cleanColumnName = parts.Last().Trim('[', ']');

                    // Find the column element to get its properties
                    var foundColumnElement = FindColumnElement(tableElement.Document!, columnName);
                    if (foundColumnElement != null)
                    {
                        var column = ParseColumnProperties(foundColumnElement, cleanColumnName);
                        columns.Add(column);
                    }
                }
            }
        }

        return columns;
    }

    private XElement? FindColumnElement(XDocument doc, string columnFullName)
    {
        return doc.Descendants(_dacNamespace + "Element")
            .FirstOrDefault(e =>
                e.Attribute("Type")?.Value == "SqlSimpleColumn" &&
                e.Attribute("Name")?.Value == columnFullName);
    }

    private ColumnDefinition ParseColumnProperties(XElement columnElement, string columnName)
    {
        var properties = columnElement.Elements(_dacNamespace + "Property")
            .ToDictionary(
                p => p.Attribute("Name")?.Value ?? "",
                p => p.Attribute("Value")?.Value ?? ""
            );

        var column = new ColumnDefinition
        {
            Name = columnName,
            SqlType = "nvarchar", // Default
            IsNullable = true,
            MaxLength = null,
            IsIdentity = false
        };

        // Parse SQL data type
        if (properties.TryGetValue("SqlDataType", out var sqlDataType))
        {
            column.SqlType = sqlDataType.ToLower();
        }

        // Parse nullability
        if (properties.TryGetValue("IsNullable", out var isNullable))
        {
            column.IsNullable = isNullable.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        // Parse length for string types
        if (properties.TryGetValue("Length", out var length) && int.TryParse(length, out var lengthValue))
        {
            column.MaxLength = lengthValue;
        }

        // Parse identity
        if (properties.TryGetValue("IsIdentity", out var isIdentity))
        {
            column.IsIdentity = isIdentity.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        // Parse precision and scale for decimal types
        if (column.SqlType == "decimal" || column.SqlType.StartsWith("decimal"))
        {
            if (properties.TryGetValue("Precision", out var precision) && int.TryParse(precision, out var precisionValue))
            {
                column.Precision = precisionValue;
            }
            if (properties.TryGetValue("Scale", out var scale) && int.TryParse(scale, out var scaleValue))
            {
                column.Scale = scaleValue;
            }
        }

        // Build full SQL type with length if applicable
        if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
        {
            column.SqlType = $"{column.SqlType}({column.MaxLength.Value})";
        }

        return column;
    }

    private HashSet<string> ParsePrimaryKey(XDocument doc, string schema, string tableName)
    {
        var primaryKeyColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fullTableName = $"[{schema}].[{tableName}]";

        // Find primary key constraints
        var pkConstraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlPrimaryKeyConstraint");

        foreach (var pkConstraint in pkConstraints)
        {
            // Check if this PK belongs to our table
            var relationshipToTable = pkConstraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

            if (relationshipToTable != null)
            {
                var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                    .Elements(_dacNamespace + "References")
                    .FirstOrDefault()?.Attribute("Name")?.Value;

                if (tableRef != null && tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
                {
                    // Get the columns that are part of this primary key
                    var columnsRelationship = pkConstraint.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "ColumnSpecifications");

                    if (columnsRelationship != null)
                    {
                        var columnRefs = columnsRelationship.Elements(_dacNamespace + "Entry")
                            .Elements(_dacNamespace + "Element")
                            .Elements(_dacNamespace + "Relationship")
                            .Where(r => r.Attribute("Name")?.Value == "Column")
                            .SelectMany(r => r.Elements(_dacNamespace + "Entry"))
                            .SelectMany(e => e.Elements(_dacNamespace + "References"))
                            .Select(r => r.Attribute("Name")?.Value)
                            .Where(n => !string.IsNullOrEmpty(n));

                        foreach (var colRef in columnRefs)
                        {
                            if (!string.IsNullOrEmpty(colRef))
                            {
                                // Extract column name
                                var parts = colRef!.Split('.');
                                var columnName = parts.Last().Trim('[', ']');
                                primaryKeyColumns.Add(columnName);
                            }
                        }
                    }
                }
            }
        }

        return primaryKeyColumns;
    }
}
