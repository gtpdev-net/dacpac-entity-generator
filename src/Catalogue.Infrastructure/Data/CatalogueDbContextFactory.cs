using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Catalogue.Infrastructure.Data;

/// <summary>Design-time factory used by EF tools (dotnet ef migrations ...).</summary>
public class CatalogueDbContextFactory : IDesignTimeDbContextFactory<CatalogueDbContext>
{
    public CatalogueDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogueDbContext>();
        optionsBuilder.UseSqlServer(
            GetConnectionString(),
            sql => sql.MigrationsAssembly(typeof(CatalogueDbContext).Assembly.FullName));

        return new CatalogueDbContext(optionsBuilder.Options);
    }

    private static string GetConnectionString()
    {
        // Resolve the base path: prefer the current directory if it contains appsettings.json
        // (e.g. when EF tools are invoked with --startup-project pointing to Catalogue.Web),
        // otherwise walk up to the solution root and fall back to the Catalogue.Web project.
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            var dir = new DirectoryInfo(basePath);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;

            if (dir != null)
                basePath = Path.Combine(dir.FullName, "src", "Catalogue.Web");
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("CatalogueDb")
            ?? throw new InvalidOperationException(
                $"Connection string 'CatalogueDb' not found. Searched in: {basePath}");
    }
}
