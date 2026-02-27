using System.Text;
using DataManager.Core.Abstractions;
using DataManager.Core.Models.Dacpac;
using DataManager.Core.Utilities;

namespace DataManager.Infrastructure.Generation;

public class EntityClassGenerator
{
    private readonly IGenerationLogger _logger;

    public EntityClassGenerator(IGenerationLogger logger)
    {
        _logger = logger;
    }

    public string GenerateEntityClass(TableDefinition table)
    {
        var sb = new StringBuilder();

        sb.AppendLine("/* This is generated code - do not modify directly */");

        // Using statements
        sb.AppendLine("using System;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        sb.AppendLine("using DataLayer.Core.Entities;");

        sb.AppendLine();

        // Namespace
        var namespaceValue = $"DataLayer.Core.Entities.{NameConverter.ToPascalCase(table.Server)}.{NameConverter.ToPascalCase(table.Database)}";
        sb.AppendLine($"namespace {namespaceValue}");
        sb.AppendLine("{");

        // Database Origin comment
        sb.AppendLine("    // This entity was generated from:");
        sb.AppendLine($"    // [{table.Server}].[{table.Database}].[dbo].[{table.TableName}]");

        // Class-level Table attribute
        sb.AppendLine($"    [Table(\"{table.TableName}\", Schema = \"{table.Database}\")]");

        // Class declaration
        var className = NameConverter.ToPascalCase(table.TableName);
        bool propertyNameConflict = table.Columns
            .Select(c => NameConverter.ToPascalCase(c.Name))
            .Any(pn => pn == className);
        if (propertyNameConflict)
        {
            className += "Entity";
        }
        sb.AppendLine($"    public class {className} : BaseEntity");
        sb.AppendLine("    {");

        // Determine if this table has a composite key
        var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
        bool isCompositeKey = pkColumns.Count > 1;

        // Properties
        bool firstProperty = true;
        foreach (var column in table.Columns)
        {
            if (!firstProperty)
            {
                sb.AppendLine(); // Empty line between properties
            }
            firstProperty = false;

            // Only add [Key] attribute for single PK
            GenerateProperty(sb, column, forceRequired: !isCompositeKey && column.IsPrimaryKey);
        }

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateProperty(StringBuilder sb, ColumnDefinition column, bool forceRequired = false)
    {
        var propertyName = NameConverter.ToPascalCase(column.Name);
        var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out bool needsMaxLength, _logger);

        // Check if this is a bool property with a default value (requires special handling)
        // Only use backing field pattern for non-nullable columns with defaults
        var isBoolWithDefault = column.SqlType.Split('(')[0].Trim().Equals("bit", StringComparison.OrdinalIgnoreCase) 
                                && !string.IsNullOrEmpty(column.DefaultValue)
                                && !column.IsComputed
                                && !column.IsNullable; // Only for non-nullable columns

        // Check if this is an int property with a default value (requires special handling)
        // Only use backing field pattern for non-nullable columns with defaults
        var sqlBaseType = column.SqlType.Split('(')[0].Trim().ToLower();
        var isIntWithDefault = (sqlBaseType == "int" || sqlBaseType == "smallint" || sqlBaseType == "tinyint" || sqlBaseType == "bigint")
                                && !string.IsNullOrEmpty(column.DefaultValue)
                                && !column.IsComputed
                                && !column.IsNullable; // Only for non-nullable columns

        // Check if this is a datetime property with default value of 0 (requires correction)
        // Only use backing field pattern for non-nullable columns with defaults
        var isDateTimeWithZeroDefault = (sqlBaseType == "datetime" || sqlBaseType == "datetime2" || sqlBaseType == "date" || sqlBaseType == "smalldatetime")
                                        && !string.IsNullOrEmpty(column.DefaultValue)
                                        && !column.IsComputed
                                        && !column.IsNullable // Only for non-nullable columns
                                        && EntityGenerationHelpers.DetermineDefaultIntValue(column.DefaultValue) == 0;

        // Add SQL default value comment if exists
        if (!string.IsNullOrEmpty(column.DefaultValue))
        {
            sb.AppendLine($"        // SQL Default: {column.DefaultValue}");
        }

        // Add computed expression comment if exists
        if (column.IsComputed && !string.IsNullOrEmpty(column.ComputedExpression))
        {
            sb.AppendLine($"        // Computed: {column.ComputedExpression}");
        }

        // Row version / concurrency token
        if (column.IsRowVersion || column.IsConcurrencyToken)
        {
            sb.AppendLine("        [Timestamp]");
        }

        // Computed column
        if (column.IsComputed)
        {
            sb.AppendLine("        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]");
        }

        // Add [Column] attribute (always)
        sb.AppendLine($"        [Column(\"{column.Name}\")]");

        // Add [Required] attribute if not nullable or if forced (for PK columns)
        // Don't add [Required] if there's a default value (EF will use the database default)
        if ((!column.IsNullable || forceRequired) && string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed)
        {
            sb.AppendLine("        [Required]");
        }

        // Add [MaxLength] attribute for string types with defined length
        if (needsMaxLength && column.MaxLength.HasValue && column.MaxLength.Value > 0)
        {
            sb.AppendLine($"        [MaxLength({column.MaxLength.Value})]");
        }
        else if (needsMaxLength)
        {
            // Try to extract max length from SqlType
            var maxLength = SqlTypeMapper.ExtractMaxLength(column.SqlType);
            if (maxLength.HasValue && maxLength.Value > 0)
            {
                sb.AppendLine($"        [MaxLength({maxLength.Value})]");
            }
        }

        // Special handling for bool properties with default values
        if (isBoolWithDefault)
        {
            // Determine the default value (true or false)
            // Safe to use ! because isBoolWithDefault already checks DefaultValue is not null or empty
            var defaultValue = EntityGenerationHelpers.DetermineDefaultBoolValue(column.DefaultValue!);
            var backingFieldName = EntityGenerationHelpers.GenerateBackingFieldName(propertyName);
            var defaultText = defaultValue ? "TRUE" : "FALSE";
            
            // Respect nullability: use csharpType which already has ? if nullable
            var propertyType = column.IsNullable ? "bool?" : "bool";
            
            // Generate property with backing field pattern
            sb.AppendLine($"        public {propertyType} {propertyName}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {backingFieldName} ?? {defaultValue.ToString().ToLower()};   // Returns {defaultText} when null (database default)");
            sb.AppendLine($"            set => {backingFieldName} = value;");
            sb.AppendLine("        }");
            sb.AppendLine($"        private bool? {backingFieldName};");
            return;
        }

        // Special handling for int properties with default values
        if (isIntWithDefault)
        {
            // Determine the default value
            // Safe to use ! because isIntWithDefault already checks DefaultValue is not null or empty
            var defaultValue = EntityGenerationHelpers.DetermineDefaultIntValue(column.DefaultValue!);
            if (defaultValue.HasValue)
            {
                var backingFieldName = EntityGenerationHelpers.GenerateBackingFieldName(propertyName);
                var csharpBaseType = csharpType.TrimEnd('?'); // Remove nullable marker if present
                
                // Respect nullability: use csharpType which already has ? if nullable
                var propertyType = column.IsNullable ? $"{csharpBaseType}?" : csharpBaseType;
                
                // Generate property with backing field pattern
                sb.AppendLine($"        public {propertyType} {propertyName}");
                sb.AppendLine("        {");
                sb.AppendLine($"            get => {backingFieldName} ?? {defaultValue.Value};   // Returns {defaultValue.Value} when null (database default)");
                sb.AppendLine($"            set => {backingFieldName} = value;");
                sb.AppendLine("        }");
                sb.AppendLine($"        private {csharpBaseType}? {backingFieldName};");
                return;
            }
        }

        // Special handling for datetime properties with default value of 0 (invalid for datetime2)
        if (isDateTimeWithZeroDefault)
        {
            var backingFieldName = EntityGenerationHelpers.GenerateBackingFieldName(propertyName);
            var csharpBaseType = csharpType.TrimEnd('?'); // Remove nullable marker if present
            
            // Respect nullability: use csharpType which already has ? if nullable
            var propertyType = column.IsNullable ? $"{csharpBaseType}?" : csharpBaseType;
            
            // Generate property with backing field pattern using DateTime.MinValue instead of 0
            sb.AppendLine($"        public {propertyType} {propertyName}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {backingFieldName} ?? DateTime.MinValue;   // Returns DateTime.MinValue when null (database default is invalid 0)");
            sb.AppendLine($"            set => {backingFieldName} = value;");
            sb.AppendLine("        }");
            sb.AppendLine($"        private {csharpBaseType}? {backingFieldName};");
            return;
        }

        // Property declaration
        string modifier = string.Empty;
        string initializer = string.Empty;
        
        // Handle string properties
        if (csharpType == "string")
        {
            if (column.IsNullable || column.IsComputed)
            {
                // Nullable string or computed
                csharpType = "string?";
            }
            else
            {
                // Non-nullable string - use 'required' if no default value
                if (string.IsNullOrEmpty(column.DefaultValue))
                {
                    modifier = "required ";
                }
            }
        }
        else
        {
            // For value types, only make nullable if the column is nullable or computed
            if ((column.IsNullable || column.IsComputed) && !csharpType.EndsWith("?"))
            {
                csharpType += "?";
            }
        }
        
        sb.AppendLine($"        public {modifier}{csharpType} {propertyName} {{ get; set; }}{initializer}");
    }

    public bool ValidateEntityClass(TableDefinition table)
    {
        // Validate class name is valid
        var className = NameConverter.ToPascalCase(table.TableName);
        if (string.IsNullOrWhiteSpace(className) || className == "_")
        {
            _logger.LogError($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Invalid class name generated for table");
            return false;
        }

        // Validate all property names are valid
        foreach (var column in table.Columns)
        {
            var propertyName = NameConverter.ToPascalCase(column.Name);
            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == "_")
            {
                _logger.LogError($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Invalid property name generated for column: {column.Name}");
                return false;
            }
        }

        // Validate namespace
        var namespaceValue = $"{NameConverter.ToPascalCase(table.Server)}.{NameConverter.ToPascalCase(table.Database)}.Entities";
        if (string.IsNullOrWhiteSpace(namespaceValue))
        {
            _logger.LogError($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Invalid namespace generated for table");
            return false;
        }

        return true;
    }

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
        else
        {
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        }

        sb.AppendLine();

        // Namespace
        var namespaceValue = $"DataLayer.Core.Entities.{NameConverter.ToPascalCase(view.Server)}.{NameConverter.ToPascalCase(view.Database)}";
        sb.AppendLine($"namespace {namespaceValue}");
        sb.AppendLine("{");

        // Database Origin comment
        sb.AppendLine("    // This view entity was generated from:");
        sb.AppendLine($"    // [{view.Server}].[{view.Database}].[{view.Schema}].[{view.ViewName}]");

        // Table attribute for view
        sb.AppendLine($"    [Table(\"{view.ViewName}\", Schema = \"{view.Database}\")]");

        // Keyless attribute if no standard audit columns
        if (!view.HasStandardAuditColumns)
        {
            sb.AppendLine("    [Keyless]");
        }

        // Class declaration
        var className = NameConverter.ToPascalCase(view.ViewName);
        bool propertyNameConflict = view.Columns
            .Select(c => NameConverter.ToPascalCase(c.Name))
            .Any(pn => pn == className);
        if (propertyNameConflict)
        {
            className += "View";
        }

        if (view.HasStandardAuditColumns)
        {
            sb.AppendLine($"    public class {className} : BaseEntity");
        }
        else
        {
            sb.AppendLine($"    public class {className}");
        }
        sb.AppendLine("    {");

        // Properties (init-only for views)
        bool firstProperty = true;
        foreach (var column in view.Columns)
        {
            if (!firstProperty)
            {
                sb.AppendLine(); // Empty line between properties
            }
            firstProperty = false;

            GenerateViewProperty(sb, column);
        }

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateViewProperty(StringBuilder sb, ColumnDefinition column)
    {
        var propertyName = NameConverter.ToPascalCase(column.Name);
        var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out bool needsMaxLength, _logger);

        // Add [Column] attribute
        sb.AppendLine($"        [Column(\"{column.Name}\")]");

        // Add [MaxLength] attribute for string types with defined length
        if (needsMaxLength && column.MaxLength.HasValue && column.MaxLength.Value > 0)
        {
            sb.AppendLine($"        [MaxLength({column.MaxLength.Value})]");
        }
        else if (needsMaxLength)
        {
            var maxLength = SqlTypeMapper.ExtractMaxLength(column.SqlType);
            if (maxLength.HasValue && maxLength.Value > 0)
            {
                sb.AppendLine($"        [MaxLength({maxLength.Value})]");
            }
        }

        // Handle nullability for init-only properties
        if (csharpType == "string" && !column.IsNullable)
        {
            csharpType = "string";
            sb.AppendLine($"        public required {csharpType} {propertyName} {{ get; init; }}");
        }
        else if (csharpType == "string")
        {
            csharpType = "string?";
            sb.AppendLine($"        public {csharpType} {propertyName} {{ get; init; }}");
        }
        else
        {
            sb.AppendLine($"        public {csharpType} {propertyName} {{ get; init; }}");
        }

        sb.AppendLine();
    }
}

