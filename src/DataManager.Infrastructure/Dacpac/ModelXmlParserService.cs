using System.Xml.Linq;
using DataManager.Core.Abstractions;
using DataManager.Core.Models.Dacpac;

namespace DataManager.Infrastructure.Dacpac;

public class ModelXmlParserService
{
    private readonly IGenerationLogger _logger;

    public ModelXmlParserService(IGenerationLogger logger)
    {
        _logger = logger;
    }

    private XNamespace _dacNamespace = "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02";

    /// <summary>
    /// Parses and validates the model.xml string once per database.
    /// Returns the XDocument to pass into the individual Parse* methods,
    /// or null if the document is invalid.
    /// </summary>
    public XDocument? PrepareDocument(string modelXml, string server, string database)
    {
        try
        {
            var doc = XDocument.Parse(modelXml);
            return ValidateDacpacFormat(doc, server, database) ? doc : null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to parse model XML: {ex.Message}");
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Script / SQL body extraction helper
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a Property element's CDATA Value child.
    /// Looks for <c>&lt;Property Name="propertyName"&gt;&lt;Value&gt;…&lt;/Value&gt;&lt;/Property&gt;</c>.
    /// </summary>
    private string? ExtractScriptProperty(XElement element, string propertyName)
    {
        var prop = element.Elements(_dacNamespace + "Property")
            .FirstOrDefault(p => p.Attribute("Name")?.Value == propertyName);

        if (prop == null)
            return null;

        // Attribute-based value (rarely used for scripts)
        var attrValue = prop.Attribute("Value")?.Value;
        if (!string.IsNullOrWhiteSpace(attrValue))
            return attrValue;

        // CDATA / inner Value element
        var valueElement = prop.Element(_dacNamespace + "Value");
        return valueElement?.Value?.Trim();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Table parsing
    // ──────────────────────────────────────────────────────────────────────────

    public TableDefinition? ParseTable(
        XDocument doc,
        string server,
        string database,
        string schema,
        string tableName,
        List<string> requiredColumns)
    {
        try
        {
            var tableElement = FindTableElement(doc, schema, tableName);
            if (tableElement == null)
            {
                _logger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Table not found in DACPAC");
                return null;
            }

            _logger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Parsing table");

            var tableDefinition = new TableDefinition
            {
                Server = server,
                Database = database,
                Schema = schema,
                TableName = tableName,
                Columns = new List<ColumnDefinition>()
            };

            var allColumns = ParseColumns(tableElement);
            var primaryKeyColumns = ParsePrimaryKey(doc, schema, tableName);
            var defaultConstraints = ParseDefaultConstraints(doc, schema, tableName);

            foreach (var column in allColumns)
            {
                var isPrimaryKey = primaryKeyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                var isRequiredByExcel = requiredColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                var isDatabaseId = column.Name.Equals("DatabaseId", StringComparison.OrdinalIgnoreCase);
                var isParentId = column.Name.Equals("ParentID", StringComparison.OrdinalIgnoreCase);

                if (isRequiredByExcel || isPrimaryKey || isDatabaseId || isParentId)
                {
                    column.IsPrimaryKey = isPrimaryKey;
                    column.IsFromExcel = isRequiredByExcel;

                    if (defaultConstraints.TryGetValue(column.Name, out var defaultValue))
                        column.DefaultValue = defaultValue;

                    tableDefinition.Columns.Add(column);
                }
            }

            foreach (var requiredCol in requiredColumns)
            {
                if (!allColumns.Any(c => c.Name.Equals(requiredCol, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Column from Excel not found in table: {requiredCol}");
                }
            }

            if (tableDefinition.Columns.Count == 0)
            {
                _logger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Table has no columns after filtering - skipping");
                return null;
            }

            if (!primaryKeyColumns.Any())
                _logger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Table has no primary key");

            _logger.LogInfo($"[{server}].[{database}].[{schema}].[{tableName}] - Table has {tableDefinition.Columns.Count} columns");

            tableDefinition.Indexes = ParseIndexes(doc, schema, tableName);
            tableDefinition.ForeignKeys = ParseForeignKeys(doc, schema, tableName);
            tableDefinition.CheckConstraints = ParseCheckConstraints(doc, schema, tableName);
            tableDefinition.UniqueConstraints = ParseUniqueConstraints(doc, schema, tableName);

            if (primaryKeyColumns.Any())
            {
                var pkColumnsList = primaryKeyColumns.ToList();
                var existingPkIndex = tableDefinition.Indexes.FirstOrDefault(idx =>
                    idx.Columns.Count == pkColumnsList.Count &&
                    idx.Columns.SequenceEqual(pkColumnsList, StringComparer.OrdinalIgnoreCase));

                if (existingPkIndex == null)
                {
                    var pkIndexName = pkColumnsList.Count == 1
                        ? $"IX_{tableName}_{pkColumnsList[0]}"
                        : $"IX_{tableName}_{string.Join("_", pkColumnsList)}";

                    tableDefinition.Indexes.Add(new IndexDefinition
                    {
                        Name = pkIndexName,
                        Columns = pkColumnsList,
                        IsUnique = true,
                        IsClustered = false,
                        IsPrimaryKeyIndex = true
                    });
                }
            }

            return tableDefinition;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}].[{schema}].[{tableName}] - Failed to parse table: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses ALL tables from the document (used by the full schema import).
    /// Uses only the DACPAC data — no Excel filter.
    /// </summary>
    public List<TableDefinition> ParseAllTables(XDocument doc, string server, string database)
    {
        var result = new List<TableDefinition>();

        var tableElements = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlTable");

        foreach (var tableElement in tableElements)
        {
            var nameAttr = tableElement.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(nameAttr)) continue;

            var parts = nameAttr.Split('.');
            if (parts.Length < 2) continue;

            var schema = parts[0].Trim('[', ']');
            var tableName = parts[1].Trim('[', ']');

            try
            {
                var allColumns = ParseColumns(tableElement);
                var primaryKeyColumns = ParsePrimaryKey(doc, schema, tableName);
                var defaultConstraints = ParseDefaultConstraints(doc, schema, tableName);

                foreach (var column in allColumns)
                {
                    var isPrimaryKey = primaryKeyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);
                    column.IsPrimaryKey = isPrimaryKey;
                    if (defaultConstraints.TryGetValue(column.Name, out var dv))
                        column.DefaultValue = dv;
                }

                var tableDefinition = new TableDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    TableName = tableName,
                    Columns = allColumns,
                    Indexes = ParseIndexes(doc, schema, tableName),
                    ForeignKeys = ParseForeignKeys(doc, schema, tableName),
                    CheckConstraints = ParseCheckConstraints(doc, schema, tableName),
                    UniqueConstraints = ParseUniqueConstraints(doc, schema, tableName)
                };

                result.Add(tableDefinition);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[{server}].[{database}].[{schema}].[{tableName}] - Error parsing table: {ex.Message}");
            }
        }

        _logger.LogInfo($"[{server}].[{database}] - Parsed {result.Count} tables");
        return result;
    }

    private XElement? FindTableElement(XDocument doc, string schema, string tableName)
    {
        var fullTableName = $"[{schema}].[{tableName}]";

        var tables = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlTable")
            .ToList();

        if (!tables.Any())
        {
            _logger.LogWarning("No SqlTable elements found in DACPAC");
            return null;
        }

        foreach (var table in tables)
        {
            var nameAttr = table.Attribute("Name")?.Value;
            if (nameAttr != null && nameAttr.Equals(fullTableName, StringComparison.OrdinalIgnoreCase))
                return table;
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
            return columns;

        foreach (var relationship in relationshipElements)
        {
            var entryElements = relationship.Elements(_dacNamespace + "Entry");
            foreach (var entry in entryElements)
            {
                var columnElement = entry.Element(_dacNamespace + "Element");
                if (columnElement != null)
                {
                    var columnType = columnElement.Attribute("Type")?.Value;
                    if (columnType == "SqlSimpleColumn" || columnType == "SqlComputedColumn")
                    {
                        var columnName = columnElement.Attribute("Name")?.Value;
                        if (string.IsNullOrEmpty(columnName)) continue;
                        var parts = columnName.Split('.');
                        var cleanColumnName = parts.Last().Trim('[', ']');
                        columns.Add(ParseColumnProperties(columnElement, cleanColumnName));
                        continue;
                    }
                }

                var references = entry.Elements(_dacNamespace + "References")
                    .Where(r => r.Attribute("Name")?.Value != null);

                foreach (var reference in references)
                {
                    var columnName = reference.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(columnName)) continue;
                    var parts = columnName.Split('.');
                    var cleanColumnName = parts.Last().Trim('[', ']');
                    var foundColumnElement = FindColumnElement(tableElement.Document!, columnName);
                    if (foundColumnElement != null)
                        columns.Add(ParseColumnProperties(foundColumnElement, cleanColumnName));
                }
            }
        }

        return columns;
    }

    private XElement? FindColumnElement(XDocument doc, string columnFullName)
    {
        var supportedColumnTypes = new[] { "SqlSimpleColumn", "SqlComputedColumn" };
        return doc.Descendants(_dacNamespace + "Element")
            .FirstOrDefault(e =>
                supportedColumnTypes.Contains(e.Attribute("Type")?.Value) &&
                e.Attribute("Name")?.Value == columnFullName);
    }

    private ColumnDefinition ParseColumnProperties(XElement columnElement, string columnName)
    {
        var properties = ReadProperties(columnElement);

        var column = new ColumnDefinition
        {
            Name = columnName,
            SqlType = "nvarchar",
            IsNullable = true
        };

        if (properties.TryGetValue("SqlDataType", out var sqlDataType))
            column.SqlType = sqlDataType.ToLower();

        if (properties.TryGetValue("IsNullable", out var isNullable))
            column.IsNullable = isNullable.Equals("True", StringComparison.OrdinalIgnoreCase);

        if (properties.TryGetValue("Length", out var length) && int.TryParse(length, out var lengthValue))
            column.MaxLength = lengthValue;

        if (properties.TryGetValue("IsIdentity", out var isIdentity))
            column.IsIdentity = isIdentity.Equals("True", StringComparison.OrdinalIgnoreCase);

        if (column.SqlType == "decimal" || column.SqlType.StartsWith("decimal"))
        {
            if (properties.TryGetValue("Precision", out var precision) && int.TryParse(precision, out var precisionValue))
                column.Precision = precisionValue;
            if (properties.TryGetValue("Scale", out var scale) && int.TryParse(scale, out var scaleValue))
                column.Scale = scaleValue;
        }

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
                var typeSpecProperties = ReadProperties(typeSpecifierElement);

                if (typeSpecProperties.TryGetValue("Length", out var tsLength) && int.TryParse(tsLength, out var tsLengthValue))
                    column.MaxLength = tsLengthValue;

                if (typeSpecProperties.TryGetValue("Precision", out var tsPrecision) && int.TryParse(tsPrecision, out var tsPrecisionValue))
                    column.Precision = tsPrecisionValue;

                if (typeSpecProperties.TryGetValue("Scale", out var tsScale) && int.TryParse(tsScale, out var tsScaleValue))
                    column.Scale = tsScaleValue;

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
                            column.SqlType = typeName.Trim('[', ']').ToLower();
                    }
                }
            }
        }

        if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
            column.SqlType = $"{column.SqlType}({column.MaxLength.Value})";
        else if (column.Precision.HasValue && column.Scale.HasValue)
            column.SqlType = $"{column.SqlType}({column.Precision.Value},{column.Scale.Value})";

        if (columnElement.Attribute("Type")?.Value == "SqlComputedColumn")
        {
            column.IsComputed = true;
            if (properties.TryGetValue("IsPersisted", out var persisted))
                column.IsComputedPersisted = persisted.Equals("True", StringComparison.OrdinalIgnoreCase);
            if (properties.TryGetValue("Expression", out var expr) || properties.TryGetValue("ExpressionScript", out expr))
                column.ComputedExpression = expr;
        }

        if (column.SqlType.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
            column.SqlType.Equals("rowversion", StringComparison.OrdinalIgnoreCase))
        {
            column.IsRowVersion = true;
            column.IsConcurrencyToken = true;
        }

        if (properties.TryGetValue("Collation", out var collation))
            column.Collation = collation;

        return column;
    }

    private HashSet<string> ParsePrimaryKey(XDocument doc, string schema, string tableName)
    {
        var primaryKeyColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fullTableName = $"[{schema}].[{tableName}]";

        var pkConstraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlPrimaryKeyConstraint");

        foreach (var pkConstraint in pkConstraints)
        {
            var relationshipToTable = pkConstraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

            if (relationshipToTable == null) continue;

            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef == null || !tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase)) continue;

            var columnsRelationship = pkConstraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "ColumnSpecifications");

            if (columnsRelationship == null) continue;

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
                    var parts = colRef!.Split('.');
                    primaryKeyColumns.Add(parts.Last().Trim('[', ']'));
                }
            }
        }

        return primaryKeyColumns;
    }

    private Dictionary<string, string> ParseDefaultConstraints(XDocument doc, string schema, string tableName)
    {
        var defaultValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fullTableName = $"[{schema}].[{tableName}]";

        var defaultConstraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlDefaultConstraint");

        foreach (var constraint in defaultConstraints)
        {
            var relationshipToTable = constraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

            if (relationshipToTable == null) continue;

            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef == null || !tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase)) continue;

            var forColumnRelationship = constraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForColumn");

            if (forColumnRelationship == null) continue;

            var columnRef = forColumnRelationship.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (string.IsNullOrEmpty(columnRef)) continue;

            var parts = columnRef.Split('.');
            var columnName = parts.Last().Trim('[', ']');

            var defaultExpression = ExtractScriptProperty(constraint, "DefaultExpressionScript");
            if (!string.IsNullOrEmpty(defaultExpression))
            {
                defaultValues[columnName] = CleanDefaultValueExpression(defaultExpression);
            }
        }

        return defaultValues;
    }

    private List<IndexDefinition> ParseIndexes(XDocument doc, string schema, string tableName)
    {
        var indexes = new List<IndexDefinition>();
        var fullTableName = $"[{schema}].[{tableName}]";

        var indexElements = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlIndex");

        foreach (var indexElement in indexElements)
        {
            var relationshipToTable = indexElement.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "IndexedObject");

            if (relationshipToTable == null) continue;

            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef == null || !tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase)) continue;

            var indexName = indexElement.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(indexName)) continue;
            var indexSimpleName = indexName.Split('.').Last().Trim('[', ']');

            var properties = indexElement.Elements(_dacNamespace + "Property")
                .ToDictionary(
                    p => p.Attribute("Name")?.Value ?? "",
                    p => p.Attribute("Value")?.Value ?? "",
                    StringComparer.OrdinalIgnoreCase);

            var isUnique = properties.TryGetValue("IsUnique", out var uniqueVal) &&
                           uniqueVal.Equals("True", StringComparison.OrdinalIgnoreCase);
            var isClustered = properties.TryGetValue("IsClustered", out var clusteredVal) &&
                              clusteredVal.Equals("True", StringComparison.OrdinalIgnoreCase);

            var indexDef = new IndexDefinition
            {
                Name = indexSimpleName,
                IsUnique = isUnique,
                IsClustered = isClustered,
                IsPrimaryKeyIndex = false
            };

            var columnsRelationship = indexElement.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "ColumnSpecifications");

            var indexColumns = new List<string>();
            if (columnsRelationship != null)
            {
                var columnSpecs = columnsRelationship.Elements(_dacNamespace + "Entry")
                    .Elements(_dacNamespace + "Element")
                    .Where(e => e.Attribute("Type")?.Value == "SqlIndexedColumnSpecification");

                foreach (var columnSpecElement in columnSpecs)
                {
                    var columnRelationship = columnSpecElement.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "Column");

                    if (columnRelationship == null) continue;

                    var columnRef = columnRelationship.Elements(_dacNamespace + "Entry")
                        .Elements(_dacNamespace + "References")
                        .FirstOrDefault()?.Attribute("Name")?.Value;

                    if (string.IsNullOrEmpty(columnRef)) continue;

                    var parts = columnRef.Split('.');
                    var columnName = parts.Last().Trim('[', ']');
                    indexColumns.Add(columnName);

                    var columnSpec = columnSpecElement.Elements(_dacNamespace + "Property")
                        .ToDictionary(
                            p => p.Attribute("Name")?.Value ?? "",
                            p => p.Attribute("Value")?.Value ?? "",
                            StringComparer.OrdinalIgnoreCase);

                    var isDescending = columnSpec.TryGetValue("IsDescending", out var descVal) &&
                                       descVal.Equals("True", StringComparison.OrdinalIgnoreCase);
                    indexDef.ColumnSortOrder[columnName] = !isDescending;
                }
            }

            indexDef.Columns = indexColumns;

            var includedColumnsRelationship = indexElement.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "IncludedColumns");
            if (includedColumnsRelationship != null)
                indexDef.IncludedColumns = ExtractColumnRefNames(includedColumnsRelationship);

            var filterScript = ExtractScriptProperty(indexElement, "FilterPredicate");
            if (!string.IsNullOrWhiteSpace(filterScript))
                indexDef.FilterDefinition = filterScript;
            else
            {
                var filterProp = indexElement.Elements(_dacNamespace + "Property")
                    .FirstOrDefault(p => p.Attribute("Name")?.Value == "FilterPredicate");
                if (filterProp != null)
                {
                    var fv = filterProp.Attribute("Value")?.Value;
                    if (!string.IsNullOrEmpty(fv)) indexDef.FilterDefinition = fv;
                }
            }

            if (indexColumns.Count > 0)
                indexes.Add(indexDef);
        }

        return indexes;
    }

    private List<ForeignKeyDefinition> ParseForeignKeys(XDocument doc, string schema, string tableName)
    {
        var foreignKeys = new List<ForeignKeyDefinition>();
        var fullTableName = $"[{schema}].[{tableName}]";

        var fkConstraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlForeignKeyConstraint");

        foreach (var fkConstraint in fkConstraints)
        {
            var relationshipToTable = fkConstraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

            if (relationshipToTable == null) continue;

            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef == null || !tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase)) continue;

            var fkName = fkConstraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";
            var fk = new ForeignKeyDefinition { Name = fkName };

            var fromColumnsRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "Columns");
            if (fromColumnsRelationship != null)
                fk.FromColumns = ExtractColumnRefNames(fromColumnsRelationship);

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

            var toColumnsRelationship = fkConstraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "ForeignColumns");
            if (toColumnsRelationship != null)
                fk.ToColumns = ExtractColumnRefNames(toColumnsRelationship);

            var properties = fkConstraint.Elements(_dacNamespace + "Property")
                .ToDictionary(
                    p => p.Attribute("Name")?.Value ?? "",
                    p => p.Attribute("Value")?.Value ?? "",
                    StringComparer.OrdinalIgnoreCase);

            if (properties.TryGetValue("DeleteAction", out var deleteAction))
                fk.OnDeleteCascade = deleteAction.Equals("CASCADE", StringComparison.OrdinalIgnoreCase);
            if (properties.TryGetValue("UpdateAction", out var updateAction))
                fk.OnUpdateCascade = updateAction.Equals("CASCADE", StringComparison.OrdinalIgnoreCase);

            fk.Cardinality = fk.FromColumns.Count == 1 && fk.ToColumns.Count == 1
                ? ForeignKeyCardinality.OneToMany
                : ForeignKeyCardinality.Unknown;

            foreignKeys.Add(fk);
        }

        return foreignKeys;
    }

    private List<CheckConstraintDefinition> ParseCheckConstraints(XDocument doc, string schema, string tableName)
    {
        var checkConstraints = new List<CheckConstraintDefinition>();
        var fullTableName = $"[{schema}].[{tableName}]";

        var constraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlCheckConstraint");

        foreach (var constraint in constraints)
        {
            var relationshipToTable = constraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

            if (relationshipToTable == null) continue;

            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef == null || !tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase)) continue;

            var checkName = constraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";
            var expression = ExtractScriptProperty(constraint, "ExpressionScript") ?? "";

            if (!string.IsNullOrEmpty(expression))
            {
                checkConstraints.Add(new CheckConstraintDefinition
                {
                    Name = checkName,
                    Expression = expression,
                    AffectedColumns = ExtractColumnNamesFromExpression(expression)
                });
            }
        }

        return checkConstraints;
    }

    private List<UniqueConstraintDefinition> ParseUniqueConstraints(XDocument doc, string schema, string tableName)
    {
        var uniqueConstraints = new List<UniqueConstraintDefinition>();
        var fullTableName = $"[{schema}].[{tableName}]";

        var constraints = doc.Descendants(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlUniqueConstraint");

        foreach (var constraint in constraints)
        {
            var relationshipToTable = constraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "DefiningTable");

            if (relationshipToTable == null) continue;

            var tableRef = relationshipToTable.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;

            if (tableRef == null || !tableRef.Equals(fullTableName, StringComparison.OrdinalIgnoreCase)) continue;

            var uniqueName = constraint.Attribute("Name")?.Value?.Split('.').Last().Trim('[', ']') ?? "";

            var properties = constraint.Elements(_dacNamespace + "Property")
                .ToDictionary(
                    p => p.Attribute("Name")?.Value ?? "",
                    p => p.Attribute("Value")?.Value ?? "",
                    StringComparer.OrdinalIgnoreCase);

            var isClustered = properties.TryGetValue("IsClustered", out var clusteredVal) &&
                              clusteredVal.Equals("True", StringComparison.OrdinalIgnoreCase);

            var columns = new List<string>();
            var columnsRelationship = constraint.Elements(_dacNamespace + "Relationship")
                .FirstOrDefault(r => r.Attribute("Name")?.Value == "ColumnSpecifications");

            if (columnsRelationship != null)
            {
                var columnSpecs = columnsRelationship.Elements(_dacNamespace + "Entry")
                    .Elements(_dacNamespace + "Element");

                foreach (var columnSpec in columnSpecs)
                {
                    var columnRelationship = columnSpec.Elements(_dacNamespace + "Relationship")
                        .FirstOrDefault(r => r.Attribute("Name")?.Value == "Column");

                    if (columnRelationship == null) continue;

                    var columnRef = columnRelationship.Elements(_dacNamespace + "Entry")
                        .Elements(_dacNamespace + "References")
                        .FirstOrDefault()?.Attribute("Name")?.Value;

                    if (!string.IsNullOrEmpty(columnRef))
                        columns.Add(columnRef.Split('.').Last().Trim('[', ']'));
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

        return uniqueConstraints;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Views
    // ──────────────────────────────────────────────────────────────────────────

    public List<ViewDefinition> ParseViews(XDocument doc, string server, string database)
    {
        try
        {
            var views = new List<ViewDefinition>();

            var viewElements = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlView");

            foreach (var viewElement in viewElements)
            {
                var viewName = viewElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(viewName)) continue;

                var parts = viewName.Split('.');
                if (parts.Length < 2) continue;

                var schema = parts[0].Trim('[', ']');
                var name = parts[1].Trim('[', ']');

                var view = new ViewDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    ViewName = name
                };

                view.Columns = ParseColumns(viewElement);
                view.HasStandardAuditColumns = HasStandardAuditColumns(view.Columns);

                // Extract SQL body: try SelectScript, fallback to QueryScript
                view.SqlBody = ExtractScriptProperty(viewElement, "SelectScript")
                            ?? ExtractScriptProperty(viewElement, "QueryScript");

                views.Add(view);
            }

            _logger.LogInfo($"[{server}].[{database}] - Parsed {views.Count} views");
            return views;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to parse views: {ex.Message}");
            return new List<ViewDefinition>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Functions
    // ──────────────────────────────────────────────────────────────────────────

    public List<FunctionDefinition> ParseUserDefinedFunctions(XDocument doc, string server, string database)
    {
        try
        {
            var functions = new List<FunctionDefinition>();

            var functionElements = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value is
                    "SqlScalarFunction" or "SqlTableValuedFunction" or "SqlInlineTableValuedFunction");

            foreach (var functionElement in functionElements)
            {
                var functionName = functionElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(functionName)) continue;

                var parts = functionName.Split('.');
                if (parts.Length < 2) continue;

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

                // Extract SQL body
                function.SqlBody = ExtractScriptProperty(functionElement, "BodyScript");

                // Extract return type
                var returnTypeRelationship = functionElement.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "ReturnType");
                if (returnTypeRelationship != null)
                {
                    var returnTypeRef = returnTypeRelationship.Elements(_dacNamespace + "Entry")
                        .Elements(_dacNamespace + "References")
                        .FirstOrDefault()?.Attribute("Name")?.Value;
                    if (!string.IsNullOrEmpty(returnTypeRef))
                        function.ReturnType = returnTypeRef.Trim('[', ']');
                }

                // Extract parameters
                function.Parameters = ParseFunctionParameters(functionElement);

                functions.Add(function);
            }

            _logger.LogInfo($"[{server}].[{database}] - Parsed {functions.Count} functions");
            return functions;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to parse functions: {ex.Message}");
            return new List<FunctionDefinition>();
        }
    }

    private List<FunctionParameter> ParseFunctionParameters(XElement parentElement)
    {
        var parameters = new List<FunctionParameter>();

        var paramsRelationship = parentElement.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "Parameters");

        if (paramsRelationship == null) return parameters;

        var paramElements = paramsRelationship.Elements(_dacNamespace + "Entry")
            .Elements(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlSubroutineParameter");

        foreach (var paramElement in paramElements)
        {
            var paramName = paramElement.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(paramName)) continue;
            var cleanParamName = paramName.Split('.').Last().Trim('[', ']');

            var properties = ReadProperties(paramElement);
            var isOutput = properties.TryGetValue("IsOutput", out var outputVal) &&
                           outputVal.Equals("True", StringComparison.OrdinalIgnoreCase);

            var defaultValue = ExtractScriptProperty(paramElement, "DefaultExpressionScript");

            // Extract SQL type from TypeSpecifier relationship
            var sqlType = ExtractParamType(paramElement);

            parameters.Add(new FunctionParameter
            {
                Name = cleanParamName,
                SqlType = sqlType,
                IsOutput = isOutput,
                DefaultValue = defaultValue
            });
        }

        return parameters;
    }

    private string ExtractParamType(XElement paramElement)
    {
        var typeSpecRel = paramElement.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "TypeSpecifier");

        if (typeSpecRel == null) return "nvarchar";

        var typeSpecElement = typeSpecRel.Elements(_dacNamespace + "Entry")
            .Elements(_dacNamespace + "Element")
            .FirstOrDefault(e => e.Attribute("Type")?.Value == "SqlTypeSpecifier");

        if (typeSpecElement == null) return "nvarchar";

        var typeSpecProperties = ReadProperties(typeSpecElement);

        var typeRel = typeSpecElement.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "Type");

        string baseType = "nvarchar";
        if (typeRel != null)
        {
            var typeRef = typeRel.Elements(_dacNamespace + "Entry")
                .Elements(_dacNamespace + "References")
                .FirstOrDefault()?.Attribute("Name")?.Value;
            if (!string.IsNullOrEmpty(typeRef))
                baseType = typeRef.Trim('[', ']').ToLower();
        }

        if (typeSpecProperties.TryGetValue("Length", out var len) && int.TryParse(len, out var lenVal))
            return $"{baseType}({lenVal})";

        if (typeSpecProperties.TryGetValue("Precision", out var prec) && typeSpecProperties.TryGetValue("Scale", out var scale))
        {
            if (int.TryParse(prec, out var pv) && int.TryParse(scale, out var sv))
                return $"{baseType}({pv},{sv})";
        }

        return baseType;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Stored Procedures
    // ──────────────────────────────────────────────────────────────────────────

    public List<StoredProcedureDefinition> ParseStoredProcedures(XDocument doc, string server, string database)
    {
        try
        {
            var procedures = new List<StoredProcedureDefinition>();

            var procElements = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlProcedure");

            foreach (var procElement in procElements)
            {
                var procName = procElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(procName)) continue;

                var parts = procName.Split('.');
                if (parts.Length < 2) continue;

                var schema = parts[0].Trim('[', ']');
                var name = parts[1].Trim('[', ']');

                var proc = new StoredProcedureDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    Name = name,
                    SqlBody = ExtractScriptProperty(procElement, "BodyScript")
                };

                // Parse parameters (reuse function parameter parsing)
                proc.Parameters = ParseProcedureParameters(procElement);

                procedures.Add(proc);
            }

            _logger.LogInfo($"[{server}].[{database}] - Parsed {procedures.Count} stored procedures");
            return procedures;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to parse stored procedures: {ex.Message}");
            return new List<StoredProcedureDefinition>();
        }
    }

    private List<ParameterDefinition> ParseProcedureParameters(XElement procElement)
    {
        var parameters = new List<ParameterDefinition>();

        var paramsRelationship = procElement.Elements(_dacNamespace + "Relationship")
            .FirstOrDefault(r => r.Attribute("Name")?.Value == "Parameters");

        if (paramsRelationship == null) return parameters;

        var paramElements = paramsRelationship.Elements(_dacNamespace + "Entry")
            .Elements(_dacNamespace + "Element")
            .Where(e => e.Attribute("Type")?.Value == "SqlSubroutineParameter");

        foreach (var paramElement in paramElements)
        {
            var paramName = paramElement.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(paramName)) continue;
            var cleanParamName = paramName.Split('.').Last().Trim('[', ']');

            var properties = ReadProperties(paramElement);
            var isOutput = properties.TryGetValue("IsOutput", out var outputVal) &&
                           outputVal.Equals("True", StringComparison.OrdinalIgnoreCase);

            var defaultValue = ExtractScriptProperty(paramElement, "DefaultExpressionScript");
            var sqlType = ExtractParamType(paramElement);

            parameters.Add(new ParameterDefinition
            {
                Name = cleanParamName,
                SqlType = sqlType,
                IsOutput = isOutput,
                DefaultValue = defaultValue
            });
        }

        return parameters;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Triggers
    // ──────────────────────────────────────────────────────────────────────────

    public List<TriggerDefinition> ParseTriggers(XDocument doc, string server, string database)
    {
        try
        {
            var triggers = new List<TriggerDefinition>();

            var triggerElements = doc.Descendants(_dacNamespace + "Element")
                .Where(e => e.Attribute("Type")?.Value == "SqlDmlTrigger");

            foreach (var triggerElement in triggerElements)
            {
                var triggerName = triggerElement.Attribute("Name")?.Value;
                if (string.IsNullOrEmpty(triggerName)) continue;

                var parts = triggerName.Split('.');
                var schema = parts.Length >= 3 ? parts[0].Trim('[', ']') : "dbo";
                var name = parts.Length >= 3
                    ? parts[1].Trim('[', ']')
                    : parts.Last().Trim('[', ']');

                // Find parent table
                string parentSchema = "";
                string parentTable = "";

                var parentRel = triggerElement.Elements(_dacNamespace + "Relationship")
                    .FirstOrDefault(r => r.Attribute("Name")?.Value == "Parent");

                if (parentRel != null)
                {
                    var parentRef = parentRel.Elements(_dacNamespace + "Entry")
                        .Elements(_dacNamespace + "References")
                        .FirstOrDefault()?.Attribute("Name")?.Value;

                    if (!string.IsNullOrEmpty(parentRef))
                    {
                        var parentParts = parentRef.Split('.');
                        if (parentParts.Length >= 2)
                        {
                            parentSchema = parentParts[0].Trim('[', ']');
                            parentTable = parentParts[1].Trim('[', ']');
                        }
                    }
                }

                triggers.Add(new TriggerDefinition
                {
                    Server = server,
                    Database = database,
                    Schema = schema,
                    Name = name,
                    SqlBody = ExtractScriptProperty(triggerElement, "BodyScript"),
                    ParentSchema = parentSchema,
                    ParentTable = parentTable
                });
            }

            _logger.LogInfo($"[{server}].[{database}] - Parsed {triggers.Count} triggers");
            return triggers;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to parse triggers: {ex.Message}");
            return new List<TriggerDefinition>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Discovery Report
    // ──────────────────────────────────────────────────────────────────────────

    public ElementDiscoveryReport GenerateDiscoveryReport(XDocument doc, string server, string database)
    {
        try
        {
            var report = new ElementDiscoveryReport { Server = server, Database = database };

            foreach (var sproc in doc.Descendants(_dacNamespace + "Element")
                         .Where(e => e.Attribute("Type")?.Value == "SqlProcedure"))
            {
                var name = sproc.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = name.Split('.');
                    var schema = parts.Length >= 2 ? parts[0].Trim('[', ']') : "";
                    var procName = parts.Length >= 2 ? parts[1].Trim('[', ']') : name.Trim('[', ']');
                    report.StoredProcedures.Add(new ElementDetail
                    {
                        Name = procName, Location = $"{schema}.{procName}",
                        Type = "StoredProcedure", Details = "User-defined stored procedure"
                    });
                }
            }

            foreach (var sequence in doc.Descendants(_dacNamespace + "Element")
                         .Where(e => e.Attribute("Type")?.Value == "SqlSequence"))
            {
                var name = sequence.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    var parts = name.Split('.');
                    var schema = parts.Length >= 2 ? parts[0].Trim('[', ']') : "";
                    var seqName = parts.Length >= 2 ? parts[1].Trim('[', ']') : name.Trim('[', ']');
                    report.Sequences.Add(new ElementDetail
                    {
                        Name = seqName, Location = $"{schema}.{seqName}",
                        Type = "Sequence", Details = "Database sequence object"
                    });
                }
            }

            foreach (var trigger in doc.Descendants(_dacNamespace + "Element")
                         .Where(e => e.Attribute("Type")?.Value == "SqlDmlTrigger"))
            {
                var name = trigger.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    report.Triggers.Add(new ElementDetail
                    {
                        Name = name.Split('.').Last().Trim('[', ']'),
                        Location = name, Type = "Trigger", Details = "DML trigger"
                    });
                }
            }

            foreach (var prop in doc.Descendants(_dacNamespace + "Element")
                         .Where(e => e.Attribute("Type")?.Value == "SqlExtendedProperty"))
            {
                var name = prop.Attribute("Name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    report.ExtendedProperties.Add(new ElementDetail
                    {
                        Name = name.Split('.').Last().Trim('[', ']'),
                        Location = name, Type = "ExtendedProperty", Details = "Extended property/comment"
                    });
                }
            }

            var allElements = doc.Descendants(_dacNamespace + "Element");
            report.ElementTypeCounts = allElements
                .GroupBy(e => e.Attribute("Type")?.Value ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());

            var handledTypes = new HashSet<string>
            {
                "SqlTable", "SqlView", "SqlSimpleColumn", "SqlComputedColumn",
                "SqlPrimaryKeyConstraint", "SqlForeignKeyConstraint", "SqlCheckConstraint",
                "SqlUniqueConstraint", "SqlDefaultConstraint", "SqlIndex",
                "SqlScalarFunction", "SqlTableValuedFunction", "SqlInlineTableValuedFunction",
                "SqlProcedure", "SqlSequence", "SqlDmlTrigger", "SqlExtendedProperty",
                "SqlTypeSpecifier", "SqlIndexedColumnSpecification", "SqlSubroutineParameter"
            };

            report.UnhandledElementTypes = report.ElementTypeCounts.Keys
                .Where(t => !handledTypes.Contains(t))
                .OrderBy(t => t)
                .ToList();

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to generate discovery report: {ex.Message}");
            return new ElementDiscoveryReport { Server = server, Database = database };
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────────────────────────────────────

    private bool ValidateDacpacFormat(XDocument doc, string server, string database)
    {
        var root = doc.Root;
        if (root == null)
        {
            _logger.LogError($"[{server}].[{database}] - Invalid DACPAC: No root element found");
            return false;
        }

        if (root.Name.LocalName != "DataSchemaModel")
        {
            _logger.LogError($"[{server}].[{database}] - Invalid DACPAC: Expected root 'DataSchemaModel', found '{root.Name.LocalName}'");
            return false;
        }

        if (root.Name.Namespace != _dacNamespace)
            _logger.LogWarning($"[{server}].[{database}] - DACPAC namespace mismatch");

        var fileFormatVersion = root.Attribute("FileFormatVersion")?.Value ?? "unknown";
        var schemaVersion = root.Attribute("SchemaVersion")?.Value ?? "unknown";
        _logger.LogInfo($"[{server}].[{database}] - DACPAC Format: FileFormatVersion={fileFormatVersion}, SchemaVersion={schemaVersion}");

        if (root.Element(_dacNamespace + "Model") == null)
        {
            _logger.LogError($"[{server}].[{database}] - Invalid DACPAC: No 'Model' element found");
            return false;
        }

        return true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private Dictionary<string, string> ReadProperties(XElement element)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in element.Elements(_dacNamespace + "Property"))
        {
            var propName = prop.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(propName)) continue;

            var propValue = prop.Attribute("Value")?.Value;
            if (string.IsNullOrEmpty(propValue))
            {
                var valueElement = prop.Element(_dacNamespace + "Value");
                if (valueElement != null)
                    propValue = valueElement.Value;
            }

            if (!string.IsNullOrEmpty(propValue))
                properties[propName] = propValue;
        }

        return properties;
    }

    private List<string> ExtractColumnRefNames(XElement? relationship)
    {
        if (relationship == null) return new List<string>();
        return relationship.Elements(_dacNamespace + "Entry")
            .Elements(_dacNamespace + "References")
            .Select(r => r.Attribute("Name")?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!.Split('.').Last().Trim('[', ']'))
            .ToList();
    }

    private string CleanDefaultValueExpression(string expression)
    {
        expression = expression.Trim();
        while (expression.StartsWith("(") && expression.EndsWith(")"))
        {
            var inner = expression.Substring(1, expression.Length - 2).Trim();
            if (!inner.Contains("(") || inner.StartsWith("'"))
                expression = inner;
            else
                break;
        }
        return expression;
    }

    private bool HasStandardAuditColumns(List<ColumnDefinition> columns)
    {
        var columnNames = columns.Select(c => c.Name.ToLowerInvariant()).ToHashSet();
        return columnNames.Contains("id") &&
               columnNames.Contains("createddate") &&
               columnNames.Contains("modifieddate");
    }

    private List<string> ExtractColumnNamesFromExpression(string expression)
    {
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
}
