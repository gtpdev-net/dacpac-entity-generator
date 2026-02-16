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

            // Validate DACPAC file format
            if (!ValidateDacpacFormat(doc, server, database))
            {
                return null;
            }

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

            // Get default constraints
            var defaultConstraints = ParseDefaultConstraints(doc, schema, tableName);

            // Process columns: include those in Excel filter plus all PK columns plus DatabaseId and ParentID
            foreach (var column in allColumns)
            {
                var isPrimaryKey = primaryKeyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                var isRequiredByExcel = requiredColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                var isDatabaseId = column.Name.Equals("DatabaseId", StringComparison.OrdinalIgnoreCase);
                var isParentId = column.Name.Equals("ParentID", StringComparison.OrdinalIgnoreCase);

                // Include if it's required by Excel OR if it's a primary key OR if it's DatabaseId OR if it's ParentID
                if (isRequiredByExcel || isPrimaryKey || isDatabaseId || isParentId)
                {
                    column.IsPrimaryKey = isPrimaryKey;
                    column.IsFromExcel = isRequiredByExcel;

                    // Apply default value if exists
                    if (defaultConstraints.TryGetValue(column.Name, out var defaultValue))
                    {
                        column.DefaultValue = defaultValue;
                    }

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

            // Parse existing indexes
            tableDefinition.Indexes = ParseIndexes(doc, schema, tableName);
            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.Indexes.Count} existing indexes");

            // Parse foreign keys
            tableDefinition.ForeignKeys = ParseForeignKeys(doc, schema, tableName);
            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.ForeignKeys.Count} foreign keys");

            // Parse check constraints
            tableDefinition.CheckConstraints = ParseCheckConstraints(doc, schema, tableName);
            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.CheckConstraints.Count} check constraints");

            // Parse unique constraints
            tableDefinition.UniqueConstraints = ParseUniqueConstraints(doc, schema, tableName);
            ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Found {tableDefinition.UniqueConstraints.Count} unique constraints");

            // Ensure primary key columns have an index
            if (primaryKeyColumns.Any())
            {
                var pkColumnsList = primaryKeyColumns.ToList();
                
                // Check if an index already exists covering the PK columns (in same order)
                var existingPkIndex = tableDefinition.Indexes.FirstOrDefault(idx => 
                    idx.Columns.Count == pkColumnsList.Count &&
                    idx.Columns.SequenceEqual(pkColumnsList, StringComparer.OrdinalIgnoreCase));

                if (existingPkIndex == null)
                {
                    // Create a unique index for the PK columns
                    var pkIndexName = pkColumnsList.Count == 1 
                        ? $"IX_{tableName}_{pkColumnsList[0]}"
                        : $"IX_{tableName}_{string.Join("_", pkColumnsList)}";

                    var pkIndex = new IndexDefinition
                    {
                        Name = pkIndexName,
                        Columns = pkColumnsList,
                        IsUnique = true,
                        IsClustered = false,
                        IsPrimaryKeyIndex = true
                    };

                    tableDefinition.Indexes.Add(pkIndex);
                    ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Added index for PK columns: {string.Join(", ", pkColumnsList)}");
                }
                else
                {
                    ConsoleLogger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Existing index '{existingPkIndex.Name}' covers PK columns");
                }
            }

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

        // Find all SqlTable elements
        var tables = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlTable")
            .ToList();

        if (!tables.Any())
        {
            ConsoleLogger.LogWarning($"No SqlTable elements found in DACPAC - the file may be using a different format");
            return null;
        }

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
            .Where(r => r.Attribute("Name")?.Value == "Columns")
            .ToList();

        if (!relationshipElements.Any())
        {
            ConsoleLogger.LogWarning($"No 'Columns' relationship found for table - table may have no columns defined");
            return columns;
        }

        foreach (var relationship in relationshipElements)
        {
            var entryElements = relationship.Elements(_dacNamespace + "Entry");
            foreach (var entry in entryElements)
            {
                // SQL Server 2017+ DACPAC format: columns are Elements of type SqlSimpleColumn inside Entry
                var columnElement = entry.Element(_dacNamespace + "Element");
                if (columnElement != null)
                {
                    var columnType = columnElement.Attribute("Type")?.Value;
                    
                    // Support both SqlSimpleColumn and SqlComputedColumn
                    if (columnType == "SqlSimpleColumn" || columnType == "SqlComputedColumn")
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
                }

                // Fallback: older DACPAC format using References
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
        // Support multiple column types
        var supportedColumnTypes = new[] { "SqlSimpleColumn", "SqlComputedColumn" };
        
        return doc.Descendants(_dacNamespace + "Element")
            .FirstOrDefault(e =>
                supportedColumnTypes.Contains(e.Attribute("Type")?.Value) &&
                e.Attribute("Name")?.Value == columnFullName);
    }

    private ColumnDefinition ParseColumnProperties(XElement columnElement, string columnName)
    {
        // Parse properties - handle both Value attributes and inner Value elements (for CDATA)
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var prop in columnElement.Elements(_dacNamespace + "Property"))
        {
            var propName = prop.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(propName))
                continue;

            // Try Value attribute first
            var propValue = prop.Attribute("Value")?.Value;
            
            // If no Value attribute, try Value element (used for CDATA or complex values)
            if (string.IsNullOrEmpty(propValue))
            {
                var valueElement = prop.Element(_dacNamespace + "Value");
                if (valueElement != null)
                {
                    propValue = valueElement.Value;
                }
            }

            if (!string.IsNullOrEmpty(propValue))
            {
                properties[propName] = propValue;
            }
        }

        var column = new ColumnDefinition
        {
            Name = columnName,
            SqlType = "nvarchar", // Default
            IsNullable = true,
            MaxLength = null,
            IsIdentity = false
        };

        // Parse SQL data type from direct properties (older DACPAC format)
        if (properties.TryGetValue("SqlDataType", out var sqlDataType))
        {
            column.SqlType = sqlDataType.ToLower();
        }

        // Parse nullability
        if (properties.TryGetValue("IsNullable", out var isNullable))
        {
            column.IsNullable = isNullable.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        // Parse length for string types (from direct properties - older format)
        if (properties.TryGetValue("Length", out var length) && int.TryParse(length, out var lengthValue))
        {
            column.MaxLength = lengthValue;
        }

        // Parse identity
        if (properties.TryGetValue("IsIdentity", out var isIdentity))
        {
            column.IsIdentity = isIdentity.Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        // Parse precision and scale for decimal types (from direct properties - older format)
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

        // Parse TypeSpecifier relationship (newer DACPAC format - SQL Server 2017+)
        // This contains the actual type information including Length, Precision, Scale
        var typeSpecifierRelationship = columnElement.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "TypeSpecifier");

        if (typeSpecifierRelationship != null)
        {
            var typeSpecifierElement = typeSpecifierRelationship
                .Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "Element")
                .FirstOrDefault(e => e.Attribute("Type")?.Value == "SqlTypeSpecifier");

            if (typeSpecifierElement != null)
            {
                // Parse TypeSpecifier properties the same way to handle CDATA
                var typeSpecProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var prop in typeSpecifierElement.Elements(_dacNamespace + "Property"))
                {
                    var propName = prop.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(propName))
                        continue;

                    var propValue = prop.Attribute("Value")?.Value;
                    if (string.IsNullOrEmpty(propValue))
                    {
                        var valueElement = prop.Element(_dacNamespace + "Value");
                        if (valueElement != null)
                        {
                            propValue = valueElement.Value;
                        }
                    }

                    if (!string.IsNullOrEmpty(propValue))
                    {
                        typeSpecProperties[propName] = propValue;
                    }
                }

                // Extract Length from TypeSpecifier
                if (typeSpecProperties.TryGetValue("Length", out var tsLength) && int.TryParse(tsLength, out var tsLengthValue))
                {
                    column.MaxLength = tsLengthValue;
                }

                // Extract Precision from TypeSpecifier
                if (typeSpecProperties.TryGetValue("Precision", out var tsPrecision) && int.TryParse(tsPrecision, out var tsPrecisionValue))
                {
                    column.Precision = tsPrecisionValue;
                }

                // Extract Scale from TypeSpecifier
                if (typeSpecProperties.TryGetValue("Scale", out var tsScale) && int.TryParse(tsScale, out var tsScaleValue))
                {
                    column.Scale = tsScaleValue;
                }

                // Extract the SQL data type from the Type relationship within TypeSpecifier
                var typeRelationship = typeSpecifierElement.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "Type");

                if (typeRelationship != null)
                {
                    var typeReference = typeRelationship
                        .Elements(_dacNamespace + "Entry")
                        .Elements(_dacNamespace + "References")
                        .FirstOrDefault();

                    if (typeReference != null)
                    {
                        var typeName = typeReference.Attribute("Name")?.Value;
                        if (!string.IsNullOrEmpty(typeName))
                        {
                            // Type name comes as "[nvarchar]" or similar, clean it up
                            column.SqlType = typeName.Trim('[', ']').ToLower();
                        }
                    }
                }
            }
        }

        // Build full SQL type with length if applicable
        if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
        {
            column.SqlType = $"{column.SqlType}({column.MaxLength.Value})";
        }
        else if (column.Precision.HasValue && column.Scale.HasValue)
        {
            column.SqlType = $"{column.SqlType}({column.Precision.Value},{column.Scale.Value})";
        }

        // Handle computed columns
        if (columnElement.Attribute("Type")?.Value == "SqlComputedColumn")
        {
            column.IsComputed = true;
            
            // Check if persisted
            if (properties.TryGetValue("IsPersisted", out var persisted))
            {
                column.IsComputedPersisted = persisted.Equals("True", StringComparison.OrdinalIgnoreCase);
            }
            
            // Get computed expression
            if (properties.TryGetValue("Expression", out var expr))
            {
                column.ComputedExpression = expr;
            }
            else if (properties.TryGetValue("ExpressionScript", out expr))
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

    private Dictionary<string, string> ParseDefaultConstraints(XDocument doc, string schema, string tableName)
    {
        var defaultValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fullTableName = $"[{schema}].[{tableName}]";

        // Find default constraint elements
        var defaultConstraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlDefaultConstraint");

        foreach (var constraint in defaultConstraints)
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
                    // Get the column this constraint applies to
                    var forColumnRelationship = constraint.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForColumn");

                    if (forColumnRelationship != null)
                    {
                        var columnRef = forColumnRelationship.Elements(_dacNamespace + "Entry")
                            .Elements(_dacNamespace + "References")
                            .FirstOrDefault()?.Attribute("Name")?.Value;

                        if (!string.IsNullOrEmpty(columnRef))
                        {
                            // Extract column name
                            var parts = columnRef.Split('.');
                            var columnName = parts.Last().Trim('[', ']');

                            // Get the default value expression
                            var defaultExpressionProperty = constraint.Elements(_dacNamespace + "Property")
                                .FirstOrDefault(p => p.Attribute("Name")?.Value == "DefaultExpressionScript");

                            if (defaultExpressionProperty != null)
                            {
                                var valueElement = defaultExpressionProperty.Element(_dacNamespace + "Value");
                                if (valueElement != null)
                                {
                                    var defaultExpression = valueElement.Value;
                                    if (!string.IsNullOrEmpty(defaultExpression))
                                    {
                                        // Clean up the expression (remove extra parentheses, etc.)
                                        defaultExpression = CleanDefaultValueExpression(defaultExpression);
                                        defaultValues[columnName] = defaultExpression;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return defaultValues;
    }

    private bool ValidateDacpacFormat(XDocument doc, string server, string database)
    {
        var root = doc.Root;

        if (root == null)
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Invalid DACPAC: No root element found");
            return false;
        }

        // Validate root element is DataSchemaModel
        if (root.Name.LocalName != "DataSchemaModel")
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Invalid DACPAC: Expected root element 'DataSchemaModel', found '{root.Name.LocalName}'");
            return false;
        }

        // Validate namespace
        if (root.Name.Namespace != _dacNamespace)
        {
            ConsoleLogger.LogWarning($"[{server}].[{database}] - DACPAC namespace mismatch: Expected '{_dacNamespace}', found '{root.Name.Namespace}'");
        }

        // Log DACPAC format information
        var fileFormatVersion = root.Attribute("FileFormatVersion")?.Value ?? "unknown";
        var schemaVersion = root.Attribute("SchemaVersion")?.Value ?? "unknown";
        var dspName = root.Attribute("DspName")?.Value ?? "unknown";

        ConsoleLogger.LogInfo($"[{server}].[{database}] - DACPAC Format: FileFormatVersion={fileFormatVersion}, SchemaVersion={schemaVersion}");

        // Extract SQL Server version from DspName (e.g., Sql140 = SQL Server 2017)
        if (dspName.Contains("Sql"))
        {
            var sqlVersionMatch = System.Text.RegularExpressions.Regex.Match(dspName, @"Sql(\d+)");
            if (sqlVersionMatch.Success)
            {
                var versionCode = sqlVersionMatch.Groups[1].Value;
                var sqlServerVersion = versionCode switch
                {
                    "90" => "SQL Server 2005",
                    "100" => "SQL Server 2008",
                    "110" => "SQL Server 2012",
                    "120" => "SQL Server 2014",
                    "130" => "SQL Server 2016",
                    "140" => "SQL Server 2017",
                    "150" => "SQL Server 2019",
                    "160" => "SQL Server 2022",
                    _ => $"SQL Server (version code {versionCode})"
                };
                ConsoleLogger.LogInfo($"[{server}].[{database}] - Target SQL Server Version: {sqlServerVersion}");
            }
        }

        // Validate that we can find the Model element
        var modelElement = root.Element(_dacNamespace + "Model");
        if (modelElement == null)
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Invalid DACPAC: No 'Model' element found");
            return false;
        }

        return true;
    }

    private List<IndexDefinition> ParseIndexes(XDocument doc, string schema, string tableName)
    {
        var indexes = new List<IndexDefinition>();
        var fullTableName = $"[{schema}].[{tableName}]";

        // Find index elements (SqlIndex)
        var indexElements = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlIndex");

        foreach (var indexElement in indexElements)
        {
            // Check if this index belongs to our table
            var relationshipToTable = indexElement.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "IndexedObject");

            if (relationshipToTable != null)
            {
                var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                    .Elements(_dacNamespace + "References")
                    .FirstOrDefault()?.Attribute("Name")?.Value;

                if (tableRef != null && tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
                {
                    var indexName = indexElement.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(indexName))
                        continue;

                    // Extract simple name (strip schema/table prefix)
                    var indexSimpleName = indexName.Split('.').Last().Trim('[', ']');

                    // Parse index properties
                    var properties = indexElement.Elements(_dacNamespace + "Property")
                        .ToDictionary(
                            p => p.Attribute("Name")?.Value ?? "",
                            p => p.Attribute("Value")?.Value ?? "",
                            StringComparer.OrdinalIgnoreCase
                        );

                    var isUnique = properties.TryGetValue("IsUnique", out var uniqueVal) && 
                                   uniqueVal.Equals("True", StringComparison.OrdinalIgnoreCase);
                    var isClustered = properties.TryGetValue("IsClustered", out var clusteredVal) && 
                                      clusteredVal.Equals("True", StringComparison.OrdinalIgnoreCase);

                    // Get the columns that are part of this index
                    var columnsRelationship = indexElement.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "ColumnSpecifications");

                    var indexColumns = new List<string>();
                    var indexDef = new IndexDefinition
                    {
                        Name = indexSimpleName,
                        IsUnique = isUnique,
                        IsClustered = isClustered,
                        IsPrimaryKeyIndex = false
                    };

                    if (columnsRelationship != null)
                    {
                        var columnSpecs = columnsRelationship.Elements(_dacNamespace + "Entry")
                            .Elements(_dacNamespace + "Element")
                            .Where(e => e.Attribute("Type")?.Value == "SqlIndexedColumnSpecification");

                        foreach (var columnSpecElement in columnSpecs)
                        {
                            var columnRelationship = columnSpecElement.Elements(_dacNamespace + "Relationship")
                                .FirstOrDefault(r => r.Attribute("Name")?.Value == "Column");

                            if (columnRelationship != null)
                            {
                                var columnRef = columnRelationship.Elements(_dacNamespace + "Entry")
                                    .Elements(_dacNamespace + "References")
                                    .FirstOrDefault()?.Attribute("Name")?.Value;

                                if (!string.IsNullOrEmpty(columnRef))
                                {
                                    var parts = columnRef.Split('.');
                                    var columnName = parts.Last().Trim('[', ']');
                                    indexColumns.Add(columnName);

                                    // Parse column specification properties for sort order
                                    var columnSpec = columnSpecElement.Elements(_dacNamespace + "Property")
                                        .ToDictionary(
                                            p => p.Attribute("Name")?.Value ?? "",
                                            p => p.Attribute("Value")?.Value ?? "",
                                            StringComparer.OrdinalIgnoreCase
                                        );

                                    var isDescending = columnSpec.TryGetValue("IsDescending", out var descVal) &&
                                                       descVal.Equals("True", StringComparison.OrdinalIgnoreCase);

                                    indexDef.ColumnSortOrder[columnName] = !isDescending; // true = ASC, false = DESC
                                }
                            }
                        }
                    }

                    indexDef.Columns = indexColumns;

                    // Get included columns
                    var includedColumnsRelationship = indexElement.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "IncludedColumns");

                    if (includedColumnsRelationship != null)
                    {
                        indexDef.IncludedColumns = ExtractColumnNames(includedColumnsRelationship);
                    }

                    // Get filter predicate
                    var filterProperty = indexElement.Elements(_dacNamespace + "Property")
                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "FilterPredicate");

                    if (filterProperty != null)
                    {
                        var filterValue = filterProperty.Attribute("Value")?.Value;
                        if (string.IsNullOrEmpty(filterValue))
                        {
                            var filterValueElement = filterProperty.Element(_dacNamespace + "Value");
                            if (filterValueElement != null)
                            {
                                filterValue = filterValueElement.Value.Trim();
                            }
                        }
                        if (!string.IsNullOrEmpty(filterValue))
                        {
                            indexDef.FilterDefinition = filterValue;
                        }
                    }

                    if (indexColumns.Count > 0)
                    {
                        indexes.Add(indexDef);
                    }
                }
            }
        }

        return indexes;
    }

    private string CleanDefaultValueExpression(string expression)
    {
        // Remove CDATA and extra whitespace
        expression = expression.Trim();

        // Remove surrounding parentheses like ((0)) -> 0
        while (expression.StartsWith("(") && expression.EndsWith(")"))
        {
            var inner = expression.Substring(1, expression.Length - 2).Trim();
            // Only remove if it's a simple value (not a function call or complex expression)
            if (!inner.Contains("(") || inner.StartsWith("'"))
            {
                expression = inner;
            }
            else
            {
                break;
            }
        }

        return expression;
    }

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

                    var fk = new ForeignKeyDefinition
                    {
                        Name = fkName
                    };

                    // Get FROM columns (this table)
                    var fromColumnsRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "Columns");
                    if (fromColumnsRelationship != null)
                    {
                        fk.FromColumns = ExtractColumnNames(fromColumnsRelationship);
                    }

                    // Get TO table reference
                    var toTableRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForeignTable");
                    
                    if (toTableRelationship != null)
                    {
                        var toTableRef = toTableRelationship.Elements(_dacNamespace + "Entry")
                            .Elements(_dacNamespace + "References")
                            .FirstOrDefault()?.Attribute("Name")?.Value;

                        if (!string.IsNullOrEmpty(toTableRef))
                        {
                            var parts = toTableRef.Split('.');
                            if (parts.Length >= 2)
                            {
                                fk.ToSchema = parts[0].Trim('[', ']');
                                fk.ToTable = parts[1].Trim('[', ']');
                            }
                        }
                    }

                    // Get TO columns (referenced table)
                    var toColumnsRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForeignColumns");
                    if (toColumnsRelationship != null)
                    {
                        fk.ToColumns = ExtractColumnNames(toColumnsRelationship);
                    }

                    // Parse properties for cascade actions
                    var properties = fkConstraint.Elements(_dacNamespace + "Property")
                        .ToDictionary(
                            p => p.Attribute("Name")?.Value ?? "",
                            p => p.Attribute("Value")?.Value ?? "",
                            StringComparer.OrdinalIgnoreCase
                        );

                    // Check delete action
                    if (properties.TryGetValue("DeleteAction", out var deleteAction))
                    {
                        fk.OnDeleteCascade = deleteAction.Equals("CASCADE", StringComparison.OrdinalIgnoreCase);
                    }

                    // Check update action
                    if (properties.TryGetValue("UpdateAction", out var updateAction))
                    {
                        fk.OnUpdateCascade = updateAction.Equals("CASCADE", StringComparison.OrdinalIgnoreCase);
                    }

                    // Infer cardinality
                    fk.Cardinality = InferCardinality(fk.FromColumns, fk.ToColumns);

                    foreignKeys.Add(fk);
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
                var viewName = viewElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(viewName))
                    continue;

                var parts = viewName.Split('.');
                if (parts.Length < 2)
                    continue;

                var schema = parts[0].Trim('[', ']');
                var name = parts[1].Trim('[', ']');

                var view = new ViewDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    ViewName = name
                };

                // Parse columns
                view.Columns = ParseColumns(viewElement);
                view.HasStandardAuditColumns = HasStandardAuditColumns(view.Columns);

                views.Add(view);
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
                    var checkName = constraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";

                    // Get the check expression
                    var expressionProperty = constraint.Elements(_dacNamespace + "Property")
                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "ExpressionScript");

                    string expression = "";
                    if (expressionProperty != null)
                    {
                        var valueElement = expressionProperty.Element(_dacNamespace + "Value");
                        if (valueElement != null)
                        {
                            expression = valueElement.Value.Trim();
                        }
                    }

                    if (!string.IsNullOrEmpty(expression))
                    {
                        var check = new CheckConstraintDefinition
                        {
                            Name = checkName,
                            Expression = expression,
                            AffectedColumns = ExtractColumnNamesFromExpression(expression)
                        };

                        checkConstraints.Add(check);
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
            .Where(s => !string.IsNullOrEmpty(s) && !IsReservedWord(s))
            .Distinct()
            .ToList();
    }

    private bool IsReservedWord(string word)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AND", "OR", "NOT", "IN", "BETWEEN", "LIKE", "IS", "NULL", "TRUE", "FALSE"
        };
        return reserved.Contains(word);
    }

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
                    var uniqueName = constraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";

                    // Parse properties
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

                    var columns = new List<string>();
                    if (columnsRelationship != null)
                    {
                        var columnSpecs = columnsRelationship.Elements(_dacNamespace + "Entry")
                            .Elements(_dacNamespace + "Element");

                        foreach (var columnSpec in columnSpecs)
                        {
                            var columnRelationship = columnSpec.Elements(_dacNamespace + "Relationship")
                                .FirstOrDefault(r => r.Attribute("Name")?.Value == "Column");

                            if (columnRelationship != null)
                            {
                                var columnRef = columnRelationship.Elements(_dacNamespace + "Entry")
                                    .Elements(_dacNamespace + "References")
                                    .FirstOrDefault()?.Attribute("Name")?.Value;

                                if (!string.IsNullOrEmpty(columnRef))
                                {
                                    columns.Add(columnRef.Split('.').Last().Trim('[', ']'));
                                }
                            }
                        }
                    }

                    if (columns.Count > 0)
                    {
                        uniqueConstraints.Add(new UniqueConstraintDefinition
                        {
                            Name = uniqueName,
                            Columns = columns,
                            IsClustered = isClustered
                        });
                    }
                }
            }
        }

        return uniqueConstraints;
    }

    public List<FunctionDefinition> ParseUserDefinedFunctions(string modelXml, string server, string database)
    {
        try
        {
            var doc = XDocument.Parse(modelXml);
            var functions = new List<FunctionDefinition>();

            // Find all function elements
            var functionElements = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlScalarFunction" ||
                           e.Attribute("Type")?.Value == "SqlTableValuedFunction" ||
                           e.Attribute("Type")?.Value == "SqlInlineTableValuedFunction");

            foreach (var functionElement in functionElements)
            {
                var functionName = functionElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(functionName))
                    continue;

                var parts = functionName.Split('.');
                if (parts.Length < 2)
                    continue;

                var schema = parts[0].Trim('[', ']');
                var name = parts[1].Trim('[', ']');

                var functionType = functionElement.Attribute("Type")?.Value;
                var type = functionType switch
                {
                    "SqlScalarFunction" => FunctionType.Scalar,
                    "SqlTableValuedFunction" => FunctionType.TableValued,
                    "SqlInlineTableValuedFunction" => FunctionType.InlineTableValued,
                    _ => FunctionType.Scalar
                };

                var function = new FunctionDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    FunctionName = name,
                    Type = type
                };

                // Parse return type (simplified)
                var returnTypeRelationship = functionElement.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "ReturnType");
                
                if (returnTypeRelationship != null)
                {
                    var returnTypeRef = returnTypeRelationship.Elements(_dacNamespace + "Entry")
                        .Elements(_dacNamespace + "References")
                        .FirstOrDefault()?.Attribute("Name")?.Value;
                    
                    if (!string.IsNullOrEmpty(returnTypeRef))
                    {
                        function.ReturnType = returnTypeRef.Trim('[', ']');
                    }
                }

                functions.Add(function);
            }

            return functions;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Failed to parse functions: {ex.Message}");
            return new List<FunctionDefinition>();
        }
    }

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

            // Find stored procedures
            var sprocs = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlProcedure");

            foreach (var sproc in sprocs)
            {
                var name = sproc.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = name.Split('.');
                    var schema = parts.Length >= 2 ? parts[0].Trim('[', ']') : "";
                    var procName = parts.Length >= 2 ? parts[1].Trim('[', ']') : name.Trim('[', ']');

                    report.StoredProcedures.Add(new ElementDetail
                    {
                        Name = procName,
                        Location = $"{schema}.{procName}",
                        Type = "StoredProcedure",
                        Details = "User-defined stored procedure"
                    });
                }
            }

            // Find sequences
            var sequences = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlSequence");

            foreach (var sequence in sequences)
            {
                var name = sequence.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = name.Split('.');
                    var schema = parts.Length >= 2 ? parts[0].Trim('[', ']') : "";
                    var seqName = parts.Length >= 2 ? parts[1].Trim('[', ']') : name.Trim('[', ']');

                    report.Sequences.Add(new ElementDetail
                    {
                        Name = seqName,
                        Location = $"{schema}.{seqName}",
                        Type = "Sequence",
                        Details = "Database sequence object"
                    });
                }
            }

            // Find triggers
            var triggers = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlDmlTrigger");

            foreach (var trigger in triggers)
            {
                var name = trigger.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    report.Triggers.Add(new ElementDetail
                    {
                        Name = name.Split('.').Last().Trim('[', ']'),
                        Location = name,
                        Type = "Trigger",
                        Details = "DML trigger"
                    });
                }
            }

            // Find extended properties
            var extendedProps = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlExtendedProperty");

            foreach (var prop in extendedProps)
            {
                var name = prop.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    report.ExtendedProperties.Add(new ElementDetail
                    {
                        Name = name.Split('.').Last().Trim('[', ']'),
                        Location = name,
                        Type = "ExtendedProperty",
                        Details = "Extended property/comment"
                    });
                }
            }

            // Count element types
            var allElements = doc.Descendants(_dacNamespace + "Element");
            var elementTypeCounts = allElements
                .GroupBy(e => e.Attribute("Type")?.Value ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            report.ElementTypeCounts = elementTypeCounts;

            // Find unhandled element types (exclude common ones we already handle)
            var handledTypes = new HashSet<string>
            {
                "SqlTable", "SqlView", "SqlSimpleColumn", "SqlComputedColumn",
                "SqlPrimaryKeyConstraint", "SqlForeignKeyConstraint", "SqlCheckConstraint",
                "SqlUniqueConstraint", "SqlDefaultConstraint", "SqlIndex",
                "SqlScalarFunction", "SqlTableValuedFunction", "SqlInlineTableValuedFunction",
                "SqlProcedure", "SqlSequence", "SqlDmlTrigger", "SqlExtendedProperty",
                "SqlTypeSpecifier", "SqlIndexedColumnSpecification"
            };

            report.UnhandledElementTypes = elementTypeCounts.Keys
                .Where(t => !handledTypes.Contains(t))
                .OrderBy(t => t)
                .ToList();

            return report;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Failed to generate discovery report: {ex.Message}");
            return new ElementDiscoveryReport { Server = server, Database = database };
        }
    }
}
