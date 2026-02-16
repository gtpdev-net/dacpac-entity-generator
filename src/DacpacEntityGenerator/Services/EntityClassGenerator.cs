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

        // Navigation Properties
        if (table.ForeignKeys.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        // Navigation Properties");
            
            foreach (var fk in table.ForeignKeys)
            {
                var navPropertyName = NameConverter.ToPascalCase(fk.ToTable);
                var navPropertyType = $"{NameConverter.ToPascalCase(fk.ToTable)}";
                
                // If it's a self-reference, suffix with the FK column name to avoid collision
                if (fk.ToTable.Equals(table.TableName, StringComparison.OrdinalIgnoreCase))
                {
                    navPropertyName = NameConverter.ToPascalCase(fk.FromColumns.FirstOrDefault() ?? "Related");
                }
                
                sb.AppendLine($"        public virtual {navPropertyType}? {navPropertyName} {{ get; set; }}");
            }
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

        // Check if this is a bool property with a default value (requires special handling)
        var isBoolWithDefault = column.SqlType.Split('(')[0].Trim().Equals("bit", StringComparison.OrdinalIgnoreCase) 
                                && !string.IsNullOrEmpty(column.DefaultValue)
                                && !column.IsComputed;

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
            var defaultValue = DetermineDefaultBoolValue(column.DefaultValue!);
            var backingFieldName = $"_{char.ToLower(propertyName[0])}{propertyName.Substring(1)}";
            
            // Generate property with backing field pattern
            sb.AppendLine($"        public bool {propertyName}");
            sb.AppendLine("        {");
            sb.AppendLine($"            get => {backingFieldName} ?? {defaultValue.ToString().ToLower()};");
            sb.AppendLine($"            set => {backingFieldName} = value;");
            sb.AppendLine("        }");
            sb.AppendLine($"        private bool? {backingFieldName};");
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

    private bool DetermineDefaultBoolValue(string defaultValue)
    {
        // Parse SQL default values like ((0)), ((1)), (0), (1), '0', '1', etc.
        if (string.IsNullOrEmpty(defaultValue))
        {
            return false;
        }
        
        // Remove parentheses and quotes
        var cleanValue = defaultValue.Trim()
            .TrimStart('(').TrimEnd(')')
            .TrimStart('(').TrimEnd(')')
            .Trim('\'', '"', ' ');
        
        // Check if it's 1 or true
        return cleanValue == "1" || cleanValue.Equals("true", StringComparison.OrdinalIgnoreCase);
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

            // Register entity (BaseEntity provides the actual key)
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>();");

            // Add index configurations
            foreach (var index in table.Indexes)
            {
                var indexColumns = string.Join(", ", index.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
                
                if (index.Columns.Count == 1)
                {
                    var indexConfig = $"modelBuilder.Entity<{fqn}>().HasIndex(e => e.{NameConverter.ToPascalCase(index.Columns[0])})";
                    
                    if (index.IsUnique)
                    {
                        indexConfig += ".IsUnique()";
                    }
                    
                    indexConfig += $".HasDatabaseName(\"{index.Name}\");";
                    sb.AppendLine($"            {indexConfig}");
                }
                else
                {
                    var indexConfig = $"modelBuilder.Entity<{fqn}>().HasIndex(e => new {{ {indexColumns} }})";
                    
                    if (index.IsUnique)
                    {
                        indexConfig += ".IsUnique()";
                    }
                    
                    indexConfig += $".HasDatabaseName(\"{index.Name}\");";
                    sb.AppendLine($"            {indexConfig}");
                }
            }

            // Add HasColumnType/HasPrecision for decimal columns and HasDefaultValueSql for columns with defaults
            foreach (var column in table.Columns)
            {
                var propertyName = NameConverter.ToPascalCase(column.Name);
                var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out _);
                
                var configurations = new List<string>();
                
                // Add decimal configuration
                if (csharpType == "decimal" || csharpType == "decimal?")
                {
                    if (column.Precision.HasValue && column.Scale.HasValue)
                    {
                        configurations.Add($"HasColumnType(\"decimal({column.Precision},{column.Scale})\")");
                    }
                    else
                    {
                        configurations.Add($"HasColumnType(\"decimal(18,2)\")");
                    }
                }
                
                // Add default value configuration
                if (!string.IsNullOrEmpty(column.DefaultValue))
                {
                    // Escape backslashes first, then double quotes
                    var escapedDefault = column.DefaultValue
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"");
                    configurations.Add($"HasDefaultValueSql(\"{escapedDefault}\")");
                }
                
                // Output the configuration if there are any
                if (configurations.Count > 0)
                {
                    var configChain = string.Join(".", configurations);
                    sb.AppendLine($"            modelBuilder.Entity<{fqn}>().Property(e => e.{propertyName}).{configChain};");
                }
            }
        }
        return sb.ToString();
    }

    // Generates a configuration class for a specific Server/Database combination
    public string GenerateEntityConfiguration(string server, string database, List<TableDefinition> tables)
    {
        var sb = new StringBuilder();

        // Using statements
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using DataLayer.Core.Entities;");
        sb.AppendLine();

        // Namespace
        var serverPascal = NameConverter.ToPascalCase(server);
        var databasePascal = NameConverter.ToPascalCase(database);
        var namespaceValue = $"DataLayer.Core.Configuration.{serverPascal}.{databasePascal}";
        sb.AppendLine($"namespace {namespaceValue}");
        sb.AppendLine("{");

        // Class declaration
        var className = $"{databasePascal}EntityConfiguration";
        sb.AppendLine($"    public static class {className}");
        sb.AppendLine("    {");

        // Configure method
        sb.AppendLine("        public static void Configure(ModelBuilder modelBuilder)");
        sb.AppendLine("        {");

        // Generate configuration for each table
        foreach (var table in tables)
        {
            var entityClassName = NameConverter.ToPascalCase(table.TableName);
            bool propertyNameConflict = table.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == entityClassName);
            if (propertyNameConflict)
            {
                entityClassName += "Entity";
            }
            var fqn = $"Core.Entities.{serverPascal}.{databasePascal}.{entityClassName}";

            // Register entity (BaseEntity provides the actual key)
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>();");

            // Add index configurations
            foreach (var index in table.Indexes)
            {
                var indexColumns = string.Join(", ", index.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
                
                string indexConfig;
                if (index.Columns.Count == 1)
                {
                    indexConfig = $"modelBuilder.Entity<{fqn}>().HasIndex(e => e.{NameConverter.ToPascalCase(index.Columns[0])})";
                }
                else
                {
                    indexConfig = $"modelBuilder.Entity<{fqn}>().HasIndex(e => new {{ {indexColumns} }})";
                }
                
                if (index.IsUnique)
                {
                    indexConfig += ".IsUnique()";
                }
                
                // Add filter if present
                if (!string.IsNullOrEmpty(index.FilterDefinition))
                {
                    var escapedFilter = index.FilterDefinition.Replace("\"", "\\\"");
                    indexConfig += $".HasFilter(\"{escapedFilter}\")";
                }
                
                indexConfig += $".HasDatabaseName(\"{index.Name}\");";
                sb.AppendLine($"            {indexConfig}");
                
                // Add comment if index has features not fully supported by HasIndex
                if (index.IncludedColumns.Any() || index.ColumnSortOrder.Any(kvp => !kvp.Value))
                {
                    sb.AppendLine($"            // Note: Index '{index.Name}' has included columns or DESC sort order - configure in migrations");
                }
            }

            // Add foreign key configurations
            foreach (var fk in table.ForeignKeys)
            {
                var toEntity = $"Core.Entities.{serverPascal}.{databasePascal}.{NameConverter.ToPascalCase(fk.ToTable)}";
                
                string fkConfig;
                if (fk.FromColumns.Count == 1)
                {
                    fkConfig = $"modelBuilder.Entity<{fqn}>().HasOne<{toEntity}>().WithMany().HasForeignKey(e => e.{NameConverter.ToPascalCase(fk.FromColumns[0])})";
                }
                else
                {
                    var fromProps = string.Join(", ", fk.FromColumns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
                    fkConfig = $"modelBuilder.Entity<{fqn}>().HasOne<{toEntity}>().WithMany().HasForeignKey(e => new {{ {fromProps} }})";
                }
                
                if (fk.OnDeleteCascade)
                {
                    fkConfig += ".OnDelete(DeleteBehavior.Cascade)";
                }
                else
                {
                    fkConfig += ".OnDelete(DeleteBehavior.Restrict)";
                }
                
                fkConfig += $".HasConstraintName(\"{fk.Name}\");";
                sb.AppendLine($"            {fkConfig}");
            }

            // Add check constraints
            foreach (var check in table.CheckConstraints)
            {
                var escapedExpr = check.Expression.Replace("\"", "\\\"");
                sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasCheckConstraint(\"{check.Name}\", \"{escapedExpr}\");");
            }

            // Add unique constraints (alternate keys)
            foreach (var unique in table.UniqueConstraints)
            {
                if (unique.Columns.Count == 1)
                {
                    sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasAlternateKey(e => e.{NameConverter.ToPascalCase(unique.Columns[0])}).HasName(\"{unique.Name}\");");
                }
                else
                {
                    var uniqueProps = string.Join(", ", unique.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
                    sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasAlternateKey(e => new {{ {uniqueProps} }}).HasName(\"{unique.Name}\");");
                }
            }

            // Column-specific configurations
            foreach (var column in table.Columns)
            {
                var propertyName = NameConverter.ToPascalCase(column.Name);
                var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out _);
                
                var configurations = new List<string>();
                
                // Add decimal configuration
                if (csharpType == "decimal" || csharpType == "decimal?")
                {
                    if (column.Precision.HasValue && column.Scale.HasValue)
                    {
                        configurations.Add($"HasColumnType(\"decimal({column.Precision},{column.Scale})\")");
                    }
                    else
                    {
                        configurations.Add($"HasColumnType(\"decimal(18,2)\")");
                    }
                }
                
                // Add collation if non-default
                if (!string.IsNullOrEmpty(column.Collation))
                {
                    configurations.Add($"UseCollation(\"{column.Collation}\")");
                }
                
                // Add computed column configuration
                if (column.IsComputed && !string.IsNullOrEmpty(column.ComputedExpression))
                {
                    var escapedExpr = column.ComputedExpression.Replace("\"", "\\\"");
                    if (column.IsComputedPersisted)
                    {
                        configurations.Add($"HasComputedColumnSql(\"{escapedExpr}\", stored: true)");
                    }
                    else
                    {
                        configurations.Add($"HasComputedColumnSql(\"{escapedExpr}\")");
                    }
                }
                
                // Add default value configuration
                if (!string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed)
                {
                    // Escape backslashes first, then double quotes
                    var escapedDefault = column.DefaultValue
                        .Replace("\\", "\\\\")
                        .Replace("\"", "\\\"");
                    configurations.Add($"HasDefaultValueSql(\"{escapedDefault}\")");
                }
                
                // Output the configuration if there are any
                if (configurations.Count > 0)
                {
                    var configChain = string.Join(".", configurations);
                    sb.AppendLine($"            modelBuilder.Entity<{fqn}>().Property(e => e.{propertyName}).{configChain};");
                }
            }

            sb.AppendLine(); // Empty line between tables
        }

        // Close method
        sb.AppendLine("        }");

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    // Generates simplified OnModelCreating body that calls configuration classes
    public string GenerateOnModelCreatingCalls(List<(string Server, string Database)> serverDatabasePairs)
    {
        var sb = new StringBuilder();
        
        foreach (var (server, database) in serverDatabasePairs.OrderBy(x => x.Server).ThenBy(x => x.Database))
        {
            var serverPascal = NameConverter.ToPascalCase(server);
            var databasePascal = NameConverter.ToPascalCase(database);
            var configClass = $"DataLayer.Core.Configuration.{serverPascal}.{databasePascal}.{databasePascal}EntityConfiguration";
            
            sb.AppendLine($"            {configClass}.Configure(modelBuilder);");
        }
        
        return sb.ToString();
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
        var csharpType = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out bool needsMaxLength);

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

    public string GenerateViewConfiguration(List<ViewDefinition> views, string server, string database)
    {
        var sb = new StringBuilder();
        var serverPascal = NameConverter.ToPascalCase(server);
        var databasePascal = NameConverter.ToPascalCase(database);

        foreach (var view in views)
        {
            var className = NameConverter.ToPascalCase(view.ViewName);
            bool propertyNameConflict = view.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == className);
            if (propertyNameConflict)
            {
                className += "View";
            }
            var fqn = $"Core.Entities.{serverPascal}.{databasePascal}.{className}";

            sb.AppendLine($"            modelBuilder.Entity<{fqn}>().ToView(\"{view.ViewName}\", \"{database}\");");
        }

        return sb.ToString();
    }
}
