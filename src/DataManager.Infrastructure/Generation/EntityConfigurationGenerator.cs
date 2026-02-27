using System.Text;
using DataManager.Core.Abstractions;
using DataManager.Core.Models.Dacpac;
using DataManager.Core.Utilities;

namespace DataManager.Infrastructure.Generation;

/// <summary>
/// Generates EF Core <c>ModelBuilder</c> configuration classes.
/// Produces both SQL Server-flavoured and SQLite-flavoured outputs from the same
/// table/view metadata so that both providers can share the same entity classes.
/// </summary>
public class EntityConfigurationGenerator
{
    private readonly IGenerationLogger _logger;

    public EntityConfigurationGenerator(IGenerationLogger logger)
    {
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SQL Server configuration
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a SQL Server EF Core configuration file for a single
    /// server/database pair.  The output namespace is
    /// <c>DataLayer.Core.Configuration.{Server}.{Database}</c> and the class is
    /// named <c>{Database}EntityConfiguration</c>.
    /// </summary>
    public string GenerateCombinedSQLConfiguration(
        string server,
        string database,
        List<TableDefinition> tables,
        List<ViewDefinition> views)
    {
        var sb = new StringBuilder();
        var serverPascal  = NameConverter.ToPascalCase(server);
        var databasePascal = NameConverter.ToPascalCase(database);

        // File header
        sb.AppendLine("/* This is generated code - do not modify directly */");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using DataLayer.Core.Entities;");
        sb.AppendLine();

        // Namespace / class / method wrapper
        sb.AppendLine($"namespace DataLayer.Core.Configuration.{serverPascal}.{databasePascal}");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {databasePascal}EntityConfiguration");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void Configure(ModelBuilder modelBuilder)");
        sb.AppendLine("        {");

        // ── Table configurations ──────────────────────────────────────────────
        if (tables.Count > 0)
        {
            foreach (var table in tables.OrderBy(t => t.TableName))
            {
                AppendSQLTableConfiguration(sb, table, serverPascal, databasePascal);
                sb.AppendLine(); // blank line between tables
            }
        }

        // ── View configurations ───────────────────────────────────────────────
        if (views.Count > 0)
        {
            sb.AppendLine("            // View Configurations");
            AppendSQLViewConfigurations(sb, views, serverPascal, databasePascal, database);
        }

        // Close method / class / namespace
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SQLite configuration
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a SQLite-compatible EF Core configuration file for a single
    /// server/database pair.  The output namespace is
    /// <c>DataLayer.Core.Configuration.{Server}.{Database}.SQLite</c> and the
    /// class is named <c>{Database}SQLiteEntityConfiguration</c>.
    /// <para>
    /// Differences from the SQL Server configuration:
    /// <list type="bullet">
    ///   <item>Every entity calls <c>.ToTable("name")</c> (no schema) to override
    ///     the <c>[Table]</c> data annotation which carries a SQL Server schema.</item>
    ///   <item>Unsupported constructs are omitted: <c>HasCheckConstraint</c>,
    ///     <c>UseCollation</c>, <c>HasFilter</c> on indexes, and
    ///     <c>HasComputedColumnSql</c>.</item>
    ///   <item>SQL Server default-value expressions are translated via
    ///     <see cref="EntityGenerationHelpers.TranslateSqlDefaultForSQLite"/>; if no
    ///     suitable translation exists the call is omitted entirely.</item>
    ///   <item>Decimal columns use <c>HasColumnType("REAL")</c> (SQLite type affinity)
    ///     instead of <c>"decimal(p,s)"</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public string GenerateCombinedSQLiteConfiguration(
        string server,
        string database,
        List<TableDefinition> tables,
        List<ViewDefinition> views)
    {
        var sb = new StringBuilder();
        var serverPascal   = NameConverter.ToPascalCase(server);
        var databasePascal = NameConverter.ToPascalCase(database);

        // File header
        sb.AppendLine("/* This is generated code - do not modify directly */");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using DataLayer.Core.Entities;");
        sb.AppendLine();

        // Namespace / class / method wrapper
        sb.AppendLine($"namespace DataLayer.Core.Configuration.{serverPascal}.{databasePascal}.SQLite");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {databasePascal}SQLiteEntityConfiguration");
        sb.AppendLine("    {");
        sb.AppendLine("        public static void Configure(ModelBuilder modelBuilder)");
        sb.AppendLine("        {");

        // ── Table configurations (SQLite) ─────────────────────────────────────
        if (tables.Count > 0)
        {
            foreach (var table in tables.OrderBy(t => t.TableName))
            {
                AppendSQLiteTableConfiguration(sb, table, serverPascal, databasePascal);
                sb.AppendLine(); // blank line between tables
            }
        }

        // ── View configurations (SQLite) ──────────────────────────────────────
        if (views.Count > 0)
        {
            sb.AppendLine("            // View Configurations");
            AppendSQLiteViewConfigurations(sb, views, serverPascal, databasePascal);
        }

        // Close method / class / namespace
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Private — SQL Server table / view helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void AppendSQLTableConfiguration(
        StringBuilder sb,
        TableDefinition table,
        string serverPascal,
        string databasePascal)
    {
        var entityClassName = NameConverter.ToPascalCase(table.TableName);
        bool propertyNameConflict = table.Columns
            .Select(c => NameConverter.ToPascalCase(c.Name))
            .Any(pn => pn == entityClassName);
        if (propertyNameConflict)
            entityClassName += "Entity";

        var fqn = $"Core.Entities.{serverPascal}.{databasePascal}.{entityClassName}";

        sb.AppendLine($"            modelBuilder.Entity<{fqn}>();");

        // Indexes
        foreach (var index in table.Indexes)
        {
            var indexColumns = string.Join(", ", index.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
            string indexConfig = index.Columns.Count == 1
                ? $"modelBuilder.Entity<{fqn}>().HasIndex(e => e.{NameConverter.ToPascalCase(index.Columns[0])})"
                : $"modelBuilder.Entity<{fqn}>().HasIndex(e => new {{ {indexColumns} }})";

            if (index.IsUnique)
                indexConfig += ".IsUnique()";

            if (!string.IsNullOrEmpty(index.FilterDefinition))
            {
                var escapedFilter = index.FilterDefinition.Replace("\"", "\\\"");
                indexConfig += $".HasFilter(\"{escapedFilter}\")";
            }

            indexConfig += $".HasDatabaseName(\"{index.Name}\");";
            sb.AppendLine($"            {indexConfig}");

            if (index.IncludedColumns.Any() || index.ColumnSortOrder.Any(kvp => !kvp.Value))
                sb.AppendLine($"            // Note: Index '{index.Name}' has included columns or DESC sort order - configure in migrations");
        }

        // Check constraints
        foreach (var check in table.CheckConstraints)
        {
            var escapedExpr = check.Expression.Replace("\"", "\\\"");
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasCheckConstraint(\"{check.Name}\", \"{escapedExpr}\");");
        }

        // Unique constraints (alternate keys)
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
            AppendSQLColumnConfiguration(sb, column, fqn);
        }
    }

    private void AppendSQLColumnConfiguration(StringBuilder sb, ColumnDefinition column, string fqn)
    {
        var propertyName   = NameConverter.ToPascalCase(column.Name);
        var csharpType     = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out _, _logger);
        var configurations = new List<string>();

        var sqlBaseType = column.SqlType.Split('(')[0].Trim().ToLower();
        var usesBoolBackingField = sqlBaseType == "bit"
            && !string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !column.IsNullable;
        var usesIntBackingField = sqlBaseType is "int" or "smallint" or "tinyint" or "bigint"
            && !string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !column.IsNullable;
        var usesDateTimeBackingField = sqlBaseType is "datetime" or "datetime2" or "date" or "smalldatetime"
            && !string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !column.IsNullable
            && EntityGenerationHelpers.DetermineDefaultIntValue(column.DefaultValue) == 0;

        if (usesBoolBackingField || usesIntBackingField || usesDateTimeBackingField)
            configurations.Add($"HasField(\"{EntityGenerationHelpers.GenerateBackingFieldName(propertyName)}\")");

        if (csharpType is "decimal" or "decimal?")
        {
            configurations.Add(column.Precision.HasValue && column.Scale.HasValue
                ? $"HasColumnType(\"decimal({column.Precision},{column.Scale})\")"
                : "HasColumnType(\"decimal(18,2)\")");
        }

        if (!string.IsNullOrEmpty(column.Collation))
            configurations.Add($"UseCollation(\"{column.Collation}\")");

        if (column.IsComputed && !string.IsNullOrEmpty(column.ComputedExpression))
        {
            var escapedExpr = column.ComputedExpression.Replace("\"", "\\\"");
            configurations.Add(column.IsComputedPersisted
                ? $"HasComputedColumnSql(\"{escapedExpr}\", stored: true)"
                : $"HasComputedColumnSql(\"{escapedExpr}\")");
        }

        if (!string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !usesDateTimeBackingField)
        {
            var escapedDefault = column.DefaultValue
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
            configurations.Add($"HasDefaultValueSql(\"{escapedDefault}\")");
        }

        if (configurations.Count > 0)
        {
            var configChain = string.Join(".", configurations);
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>().Property(e => e.{propertyName}).{configChain};");
        }
    }

    private static void AppendSQLViewConfigurations(
        StringBuilder sb,
        List<ViewDefinition> views,
        string serverPascal,
        string databasePascal,
        string database)
    {
        foreach (var view in views)
        {
            var className = NameConverter.ToPascalCase(view.ViewName);
            bool propertyNameConflict = view.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == className);
            if (propertyNameConflict)
                className += "View";

            var fqn = $"Core.Entities.{serverPascal}.{databasePascal}.{className}";
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>().ToView(\"{view.ViewName}\", \"{database}\");");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Private — SQLite table / view helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void AppendSQLiteTableConfiguration(
        StringBuilder sb,
        TableDefinition table,
        string serverPascal,
        string databasePascal)
    {
        var entityClassName = NameConverter.ToPascalCase(table.TableName);
        bool propertyNameConflict = table.Columns
            .Select(c => NameConverter.ToPascalCase(c.Name))
            .Any(pn => pn == entityClassName);
        if (propertyNameConflict)
            entityClassName += "Entity";

        var fqn = $"Core.Entities.{serverPascal}.{databasePascal}.{entityClassName}";

        // Override [Table] schema attribute — SQLite has no schemas
        sb.AppendLine($"            modelBuilder.Entity<{fqn}>().ToTable(\"{table.TableName}\");");

        // Indexes (no filtered indexes for SQLite)
        foreach (var index in table.Indexes)
        {
            var indexColumns = string.Join(", ", index.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
            string indexConfig = index.Columns.Count == 1
                ? $"modelBuilder.Entity<{fqn}>().HasIndex(e => e.{NameConverter.ToPascalCase(index.Columns[0])})"
                : $"modelBuilder.Entity<{fqn}>().HasIndex(e => new {{ {indexColumns} }})";

            if (index.IsUnique)
                indexConfig += ".IsUnique()";

            // Omit HasFilter — SQLite doesn't support filtered indexes via EF Core
            // Omit HasDatabaseName — index names are less significant in SQLite

            sb.AppendLine($"            {indexConfig};");
        }

        // Unique constraints (alternate keys) — supported by SQLite
        foreach (var unique in table.UniqueConstraints)
        {
            if (unique.Columns.Count == 1)
            {
                sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasAlternateKey(e => e.{NameConverter.ToPascalCase(unique.Columns[0])});");
            }
            else
            {
                var uniqueProps = string.Join(", ", unique.Columns.Select(c => $"e.{NameConverter.ToPascalCase(c)}"));
                sb.AppendLine($"            modelBuilder.Entity<{fqn}>().HasAlternateKey(e => new {{ {uniqueProps} }});");
            }
        }

        // Column-specific configurations (SQLite dialect)
        foreach (var column in table.Columns)
        {
            AppendSQLiteColumnConfiguration(sb, column, fqn);
        }
    }

    private void AppendSQLiteColumnConfiguration(StringBuilder sb, ColumnDefinition column, string fqn)
    {
        var propertyName   = NameConverter.ToPascalCase(column.Name);
        var csharpType     = SqlTypeMapper.MapToCSharpType(column.SqlType, column.IsNullable, out _, _logger);
        var configurations = new List<string>();

        var sqlBaseType = column.SqlType.Split('(')[0].Trim().ToLower();
        var usesBoolBackingField = sqlBaseType == "bit"
            && !string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !column.IsNullable;
        var usesIntBackingField = sqlBaseType is "int" or "smallint" or "tinyint" or "bigint"
            && !string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !column.IsNullable;
        var usesDateTimeBackingField = sqlBaseType is "datetime" or "datetime2" or "date" or "smalldatetime"
            && !string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !column.IsNullable
            && EntityGenerationHelpers.DetermineDefaultIntValue(column.DefaultValue) == 0;

        // Backing field configuration — same as SQL Server
        if (usesBoolBackingField || usesIntBackingField || usesDateTimeBackingField)
            configurations.Add($"HasField(\"{EntityGenerationHelpers.GenerateBackingFieldName(propertyName)}\")");

        // SQLite type affinity: use REAL instead of decimal(p,s)
        if (csharpType is "decimal" or "decimal?")
            configurations.Add("HasColumnType(\"REAL\")");

        // Omit: UseCollation — not supported as a column-level EF Core config for SQLite
        // Omit: HasComputedColumnSql — limited / no EF Core SQLite support for generated columns

        // Default values — translate SQL Server expressions to SQLite equivalents
        if (!string.IsNullOrEmpty(column.DefaultValue) && !column.IsComputed && !usesDateTimeBackingField)
        {
            var sqliteDefault = EntityGenerationHelpers.TranslateSqlDefaultForSQLite(column.DefaultValue);
            if (sqliteDefault != null)
            {
                var escapedDefault = sqliteDefault
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");
                configurations.Add($"HasDefaultValueSql(\"{escapedDefault}\")");
            }
            // If translation returned null, omit HasDefaultValueSql (SQL Server-specific expression)
        }

        if (configurations.Count > 0)
        {
            var configChain = string.Join(".", configurations);
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>().Property(e => e.{propertyName}).{configChain};");
        }
    }

    private static void AppendSQLiteViewConfigurations(
        StringBuilder sb,
        List<ViewDefinition> views,
        string serverPascal,
        string databasePascal)
    {
        foreach (var view in views)
        {
            var className = NameConverter.ToPascalCase(view.ViewName);
            bool propertyNameConflict = view.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == className);
            if (propertyNameConflict)
                className += "View";

            var fqn = $"Core.Entities.{serverPascal}.{databasePascal}.{className}";
            // SQLite: ToView with no schema
            sb.AppendLine($"            modelBuilder.Entity<{fqn}>().ToView(\"{view.ViewName}\");");
        }
    }
}
