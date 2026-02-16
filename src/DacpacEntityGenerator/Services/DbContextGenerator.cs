using System.Text;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class DbContextGenerator
{
    public string GenerateDacpacDbContext(
        List<TableDefinition> allTables,
        List<ViewDefinition> allViews,
        List<(string Server, string Database)> serverDatabasePairs)
    {
        var sb = new StringBuilder();

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

        // Namespace declaration
        sb.AppendLine("namespace DataLayer.Infrastructure.Persistence.Contexts");
        sb.AppendLine("{");

        // Class declaration
        sb.AppendLine("    public partial class DacpacDbContext(DbContextOptions<DacpacDbContext> options) : BaseDbContext(options)");
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

                    sb.AppendLine($"        public DbSet<{fullyQualifiedType}> {pluralName} {{ get; set; }} = null!;");
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

                    sb.AppendLine($"        public DbSet<{fullyQualifiedType}> {pluralName} {{ get; set; }} = null!;");
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
}
