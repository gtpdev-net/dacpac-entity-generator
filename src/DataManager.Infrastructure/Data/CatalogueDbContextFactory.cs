using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataManager.Infrastructure.Data;

/// <summary>Design-time factory used by EF tools (dotnet ef migrations ...).</summary>
public class DataManagerDbContextFactory : IDesignTimeDbContextFactory<DataManagerDbContext>
{
    public DataManagerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DataManagerDbContext>();
        optionsBuilder.UseSqlServer(
            GetConnectionString(),
            sql => sql.MigrationsAssembly(typeof(DataManagerDbContext).Assembly.FullName));

        return new DataManagerDbContext(optionsBuilder.Options);
    }

    private static string GetConnectionString()
    {
        // Resolve the base path: prefer the current directory if it contains appsettings.json
        // (e.g. when EF tools are invoked with --startup-project pointing to DataManager.Web),
        // otherwise walk up to the solution root and fall back to the DataManager.Web project.
        var basePath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(basePath, "appsettings.json")))
        {
            var dir = new DirectoryInfo(basePath);
            while (dir != null && !dir.GetFiles("*.sln").Any())
                dir = dir.Parent;

            if (dir != null)
                basePath = Path.Combine(dir.FullName, "src", "DataManager.Web");
        }

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DataManagerDb")
            ?? throw new InvalidOperationException(
                $"Connection string 'DataManagerDb' not found. Searched in: {basePath}");
    }
}
