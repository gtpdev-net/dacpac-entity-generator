using System.Text;
using DataManager.Core.Models.Dacpac;
using DataManager.Core.Utilities;

namespace DataManager.Infrastructure.Generation;

public class DbContextGenerator
{
    private record DbContextVariant(
        string Namespace,
        string ClassName,
        string ConfigNsSuffix,
        string ConfigClassSuffix);

    public string GenerateSQLDbContext(
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews,
        List<(string Server, string Database)> serverDatabasePairs)
    {
        var variant = new DbContextVariant(
            Namespace: "DataLayer.Infrastructure.Persistence.Contexts",
            ClassName: "SQLDbContext",
            ConfigNsSuffix: "",
            ConfigClassSuffix: "EntityConfiguration");

        return GenerateDbContext(variant, allTables, allViews, serverDatabasePairs);
    }

    /// <summary>
    /// Generates a SQLite <c>DbContext</c> file.  The class is named
    /// <c>SQLiteDbContext</c>, lives in the
    /// <c>DataLayer.Infrastructure.Persistence.Contexts.SQLite</c> namespace, and
    /// references the <c>{Database}SQLiteEntityConfiguration</c> classes produced by
    /// <c>EntityConfigurationGenerator.GenerateCombinedSQLiteConfiguration</c>.
    /// The DbSet properties are identical to those in the SQL Server variant because
    /// both contexts use the same shared entity classes.
    /// </summary>
    public string GenerateSQLiteDbContext(
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews,
        List<(string Server, string Database)> serverDatabasePairs)
    {
        var variant = new DbContextVariant(
            Namespace: "DataLayer.Infrastructure.Persistence.Contexts.SQLite",
            ClassName: "SQLiteDbContext",
            ConfigNsSuffix: ".SQLite",
            ConfigClassSuffix: "SQLiteEntityConfiguration");

        return GenerateDbContext(variant, allTables, allViews, serverDatabasePairs);
    }

    private string GenerateDbContext(
        DbContextVariant variant,
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews,
        List<(string Server, string Database)> serverDatabasePairs)
    {
        var sb = new StringBuilder();

        sb.AppendLine(FileWriterService.GeneratedFileTag);

        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using DataLayer.Infrastructure.Persistence.Contexts.Base;");
        sb.AppendLine();

        var entityNamespaces = new HashSet<string>();

        foreach (var table in allTables)
        {
            entityNamespaces.Add($"DataLayer.Core.Entities.{NameConverter.ToPascalCase(table.Server)}.{NameConverter.ToPascalCase(table.Database)}");
        }

        foreach (var view in allViews)
        {
            entityNamespaces.Add($"DataLayer.Core.Entities.{NameConverter.ToPascalCase(view.Server)}.{NameConverter.ToPascalCase(view.Database)}");
        }

        foreach (var (server, database) in serverDatabasePairs)
        {
            entityNamespaces.Add($"DataLayer.Core.Configuration.{NameConverter.ToPascalCase(server)}.{NameConverter.ToPascalCase(database)}{variant.ConfigNsSuffix}");
        }

        foreach (var ns in entityNamespaces.OrderBy(x => x))
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();

        var conflictingNames = DetectDbSetNameConflicts(allTables, allViews);

        sb.AppendLine($"namespace {variant.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    public partial class {variant.ClassName}(DbContextOptions<{variant.ClassName}> options) : BaseDbContext(options)");
        sb.AppendLine("    {");

        if (allTables.Count > 0)
        {
            AppendDbSetProperties(sb,
                allTables.Select(t => (t.Server, t.Database, Name: t.TableName, Columns: t.Columns.Select(c => c.Name).ToList())),
                conflictSuffix: "Entity", sectionComment: "Table Entity DbSets", conflictingNames);
        }

        if (allViews.Count > 0)
        {
            AppendDbSetProperties(sb,
                allViews.Select(v => (v.Server, v.Database, Name: v.ViewName, Columns: v.Columns.Select(c => c.Name).ToList())),
                conflictSuffix: "View", sectionComment: "View Entity DbSets", conflictingNames);
        }

        sb.AppendLine();
        sb.AppendLine("        protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("        {");
        sb.AppendLine("            base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        if (serverDatabasePairs.Count > 0)
        {
            sb.AppendLine("            // Entity Configurations");
            foreach (var (_, database) in serverDatabasePairs.OrderBy(x => x.Server).ThenBy(x => x.Database))
            {
                sb.AppendLine($"            {NameConverter.ToPascalCase(database)}{variant.ConfigClassSuffix}.Configure(modelBuilder);");
            }
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void AppendDbSetProperties(
        StringBuilder sb,
        IEnumerable<(string Server, string Database, string Name, List<string> Columns)> items,
        string conflictSuffix,
        string sectionComment,
        HashSet<string> conflictingNames)
    {
        sb.AppendLine();
        sb.AppendLine($"        // {sectionComment}");

        var grouped = items
            .GroupBy(i => new { i.Server, i.Database })
            .OrderBy(g => g.Key.Server)
            .ThenBy(g => g.Key.Database);

        foreach (var group in grouped)
        {
            var serverPascal   = NameConverter.ToPascalCase(group.Key.Server);
            var databasePascal = NameConverter.ToPascalCase(group.Key.Database);

            sb.AppendLine();
            sb.AppendLine($"        // [{group.Key.Server}].[{group.Key.Database}]");

            foreach (var item in group.OrderBy(i => i.Name))
            {
                var className = NameConverter.ToPascalCase(item.Name);

                bool propertyNameConflict = item.Columns
                    .Select(NameConverter.ToPascalCase)
                    .Any(pn => pn == className);
                if (propertyNameConflict)
                    className += conflictSuffix;

                var dbSetName         = className + "DbSet";
                var dbSetPropertyName = conflictingNames.Contains(dbSetName)
                    ? $"{databasePascal}{dbSetName}"
                    : dbSetName;

                sb.AppendLine($"        public DbSet<DataLayer.Core.Entities.{serverPascal}.{databasePascal}.{className}> {dbSetPropertyName} {{ get; set; }} = null!;");
            }
        }
    }

    private static HashSet<string> DetectDbSetNameConflicts(
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews)
    {
        var nameUsages = new Dictionary<string, HashSet<(string Server, string Database)>>();

        var allItems =
            allTables.Select(t => (t.Server, t.Database, Name: t.TableName, Columns: t.Columns.Select(c => c.Name).ToList(), ConflictSuffix: "Entity"))
            .Concat(
            allViews.Select(v  => (v.Server, v.Database, Name: v.ViewName,  Columns: v.Columns.Select(c => c.Name).ToList(), ConflictSuffix: "View")));

        foreach (var item in allItems)
        {
            var className = NameConverter.ToPascalCase(item.Name);

            bool propertyNameConflict = item.Columns
                .Select(NameConverter.ToPascalCase)
                .Any(pn => pn == className);
            if (propertyNameConflict)
                className += item.ConflictSuffix;

            var dbSetName = className + "DbSet";

            if (!nameUsages.TryGetValue(dbSetName, out var set))
                nameUsages[dbSetName] = set = new HashSet<(string, string)>();

            set.Add((item.Server, item.Database));
        }

        return nameUsages
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }
}
