using System.Text;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class EntityClassGenerator
{
    public string GenerateEntityClass(TableDefinition table)
    {
        var sb = new StringBuilder();

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
        var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out bool needsMaxLength);

        // Add [Column] attribute (always)
        sb.AppendLine($"        [Column(\"{column.Name}\")]");

        // Add [Required] attribute if not nullable or if forced (for PK columns)
        if (!column.IsNullable || forceRequired)
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

        // Property declaration: use 'required' for non-nullable reference types, '?' for nullable
        string modifier = string.Empty;
        if (!column.IsNullable && csharpType == "string")
        {
            modifier = "required ";
        }
        else if (column.IsNullable && csharpType == "string")
        {
            csharpType = "string?";
        }
        sb.AppendLine($"        public {modifier}{csharpType} {propertyName} {{ get; set; }}");
    }

    public bool ValidateEntityClass(TableDefinition table)
    {
        // Validate class name is valid
        var className = NameConverter.ToPascalCase(table.TableName);
        if (string.IsNullOrWhiteSpace(className) || className == "_")
        {
            ConsoleLogger.LogError($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Invalid class name generated for table");
            return false;
        }

        // Validate all property names are valid
        foreach (var column in table.Columns)
        {
            var propertyName = NameConverter.ToPascalCase(column.Name);
            if (string.IsNullOrWhiteSpace(propertyName) || propertyName == "_")
            {
                ConsoleLogger.LogError($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Invalid property name generated for column: {column.Name}");
                return false;
            }
        }

        // Validate namespace
        var namespaceValue = $"{NameConverter.ToPascalCase(table.Server)}.{NameConverter.ToPascalCase(table.Database)}.Entities";
        if (string.IsNullOrWhiteSpace(namespaceValue))
        {
            ConsoleLogger.LogError($"[{table.Server}].[{table.Database}].[{table.Schema}].[{table.TableName}] - Invalid namespace generated for table");
            return false;
        }

        return true;
    }

    // Generates the OnModelCreating body for a list of TableDefinitions
    public string GenerateOnModelCreatingBody(List<TableDefinition> tables)
    {
        var sb = new StringBuilder();
        foreach (var table in tables)
        {
            var className = NameConverter.ToPascalCase(table.TableName);
            bool propertyNameConflict = table.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == className);
            if (propertyNameConflict)
            {
                className += "Entity";
            }
            var fqn = $"Core.Entities.{table.Server}.{table.Database}.{className}";
            var pkColumns = table.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (pkColumns.Count == 1)
            {
                sb.AppendLine($"            modelBuilder.Entity<{fqn}>();");
            }
            else if (pkColumns.Count > 1)
            {
                var keyProps = string.Join(", ", pkColumns.Select(c => $"e.{NameConverter.ToPascalCase(c.Name)}"));
                sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasKey(e => new {{ {keyProps} }});");
            }

            // Add HasColumnType/HasPrecision for decimal columns
            foreach (var column in table.Columns)
            {
                var propertyName = NameConverter.ToPascalCase(column.Name);
                var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out _);
                if (csharpType == "decimal" || csharpType == "decimal?")
                {
                    if (column.Precision.HasValue && column.Scale.HasValue)
                    {
                        sb.AppendLine($"            modelBuilder.Entity<{fqn}>().Property(e => e.{propertyName}).HasColumnType(\"decimal({column.Precision},{column.Scale})\");");
                    }
                    else
                    {
                        sb.AppendLine($"            modelBuilder.Entity<{fqn}>().Property(e => e.{propertyName}).HasColumnType(\"decimal(18,2)\");");
                    }
                }
            }
        }
        return sb.ToString();
    }
}
