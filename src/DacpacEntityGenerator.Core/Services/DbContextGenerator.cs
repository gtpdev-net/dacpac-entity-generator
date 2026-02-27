using System.Text;
using Dacpac.Management.Models;
using Dacpac.Management.Utilities;

namespace DacpacEntityGenerator.Core.Services;

public class DbContextGenerator
{
    public string GenerateSQLDbContext(
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews,
        List<(string Server, string Database)> serverDatabasePairs)
    {
        var sb = new StringBuilder();

        sb.AppendLine("/* This is generated code - do not modify directly */");

        // Generate using statements
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using DataLayer.Infrastructure.Persistence.Contexts.Base;");
        sb.AppendLine();

        // Generate using statements for all entity namespaces
        var entityNamespaces = new HashSet<string>();
        
        foreach (var table in allTables)
        {
            var ns = $"DataLayer.Core.Entities.{NameConverter.ToPascalCase(table.Server)}.{NameConverter.ToPascalCase(table.Database)}";
            entityNamespaces.Add(ns);
        }

        foreach (var view in allViews)
        {
            var ns = $"DataLayer.Core.Entities.{NameConverter.ToPascalCase(view.Server)}.{NameConverter.ToPascalCase(view.Database)}";
            entityNamespaces.Add(ns);
        }

        // Add using statements for configuration namespaces
        foreach (var (server, database) in serverDatabasePairs)
        {
            var serverPascal = NameConverter.ToPascalCase(server);
            var databasePascal = NameConverter.ToPascalCase(database);
            var configNs = $"DataLayer.Core.Configuration.{serverPascal}.{databasePascal}";
            entityNamespaces.Add(configNs);
        }

        foreach (var ns in entityNamespaces.OrderBy(x => x))
        {
            sb.AppendLine($"using {ns};");
        }

        sb.AppendLine();

        // Detect naming conflicts - find entity names that appear in multiple databases
        var conflictingNames = DetectDbSetNameConflicts(allTables, allViews);

        // Namespace declaration
        sb.AppendLine("namespace DataLayer.Infrastructure.Persistence.Contexts");
        sb.AppendLine("{");

        // Class declaration
        sb.AppendLine("    public partial class SQLDbContext(DbContextOptions<SQLDbContext> options) : BaseDbContext(options)");
        sb.AppendLine("    {");

        // Generate DbSet properties for all tables
        if (allTables.Count > 0)
        {
            sb.AppendLine("        // Table Entity DbSets");
            var groupedTables = allTables
                .GroupBy(t => new { t.Server, t.Database })
                .OrderBy(g => g.Key.Server)
                .ThenBy(g => g.Key.Database);

            foreach (var group in groupedTables)
            {
                var serverPascal = NameConverter.ToPascalCase(group.Key.Server);
                var databasePascal = NameConverter.ToPascalCase(group.Key.Database);

                sb.AppendLine();
                sb.AppendLine($"        // [{group.Key.Server}].[{group.Key.Database}]");

                foreach (var table in group.OrderBy(t => t.TableName))
                {
                    var className = NameConverter.ToPascalCase(table.TableName);
                    
                    // Check if we added "Entity" suffix during generation
                    bool propertyNameConflict = table.Columns
                        .Select(c => NameConverter.ToPascalCase(c.Name))
                        .Any(pn => pn == className);
                    if (propertyNameConflict)
                    {
                        className += "Entity";
                    }

                    var fullyQualifiedType = $"{serverPascal}.{databasePascal}.{className}";
                    var pluralName = NameConverter.Pluralize(className);
                    
                    // Add database prefix only if there's a naming conflict
                    var dbSetPropertyName = conflictingNames.Contains(pluralName)
                        ? $"{databasePascal}{pluralName}"
                        : pluralName;

                    sb.AppendLine($"        public DbSet<DataLayer.Core.Entities.{fullyQualifiedType}> {dbSetPropertyName} {{ get; set; }} = null!;");
                }
            }
        }

        // Generate DbSet properties for all views
        if (allViews.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        // View Entity DbSets");
            var groupedViews = allViews
                .GroupBy(v => new { v.Server, v.Database })
                .OrderBy(g => g.Key.Server)
                .ThenBy(g => g.Key.Database);

            foreach (var group in groupedViews)
            {
                var serverPascal = NameConverter.ToPascalCase(group.Key.Server);
                var databasePascal = NameConverter.ToPascalCase(group.Key.Database);

                sb.AppendLine();
                sb.AppendLine($"        // [{group.Key.Server}].[{group.Key.Database}]");

                foreach (var view in group.OrderBy(v => v.ViewName))
                {
                    var className = NameConverter.ToPascalCase(view.ViewName);
                    
                    // Check if we added "View" suffix during generation
                    bool propertyNameConflict = view.Columns
                        .Select(c => NameConverter.ToPascalCase(c.Name))
                        .Any(pn => pn == className);
                    if (propertyNameConflict)
                    {
                        className += "View";
                    }

                    var fullyQualifiedType = $"{serverPascal}.{databasePascal}.{className}";
                    var pluralName = NameConverter.Pluralize(className);
                    
                    // Add database prefix only if there's a naming conflict
                    var dbSetPropertyName = conflictingNames.Contains(pluralName)
                        ? $"{databasePascal}{pluralName}"
                        : pluralName;

                    sb.AppendLine($"        public DbSet<DataLayer.Core.Entities.{fullyQualifiedType}> {dbSetPropertyName} {{ get; set; }} = null!;");
                }
            }
        }

        sb.AppendLine();

        // OnModelCreating method
        sb.AppendLine("        protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("        {");
        sb.AppendLine("            base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        // Generate configuration calls
        if (serverDatabasePairs.Count > 0)
        {
            sb.AppendLine("            // Entity Configurations");
            foreach (var (server, database) in serverDatabasePairs.OrderBy(x => x.Server).ThenBy(x => x.Database))
            {
                var serverPascal = NameConverter.ToPascalCase(server);
                var databasePascal = NameConverter.ToPascalCase(database);
                var configClass = $"{databasePascal}EntityConfiguration";
                
                sb.AppendLine($"            {configClass}.Configure(modelBuilder);");
            }
        }

        sb.AppendLine("        }");

        // Close class
        sb.AppendLine("    }");

        // Close namespace
        sb.AppendLine("}");

        return sb.ToString();
    }

    private HashSet<string> DetectDbSetNameConflicts(
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews)
    {
        // Track pluralized names and which server/database combinations they appear in
        var nameUsages = new Dictionary<string, HashSet<(string Server, string Database)>>();

        // Analyze tables
        foreach (var table in allTables)
        {
            var className = NameConverter.ToPascalCase(table.TableName);
            
            // Check if "Entity" suffix was added
            bool propertyNameConflict = table.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == className);
            if (propertyNameConflict)
            {
                className += "Entity";
            }

            var pluralName = NameConverter.Pluralize(className);
            
            if (!nameUsages.ContainsKey(pluralName))
            {
                nameUsages[pluralName] = new HashSet<(string, string)>();
            }
            nameUsages[pluralName].Add((table.Server, table.Database));
        }

        // Analyze views
        foreach (var view in allViews)
        {
            var className = NameConverter.ToPascalCase(view.ViewName);
            
            // Check if "View" suffix was added
            bool propertyNameConflict = view.Columns
                .Select(c => NameConverter.ToPascalCase(c.Name))
                .Any(pn => pn == className);
            if (propertyNameConflict)
            {
                className += "View";
            }

            var pluralName = NameConverter.Pluralize(className);
            
            if (!nameUsages.ContainsKey(pluralName))
            {
                nameUsages[pluralName] = new HashSet<(string, string)>();
            }
            nameUsages[pluralName].Add((view.Server, view.Database));
        }

        // Return names that appear in more than one server/database combination
        return nameUsages
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key)
            .ToHashSet();
    }
}
