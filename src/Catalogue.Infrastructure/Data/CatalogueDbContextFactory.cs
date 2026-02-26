using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalogue.Infrastructure.Data;

/// <summary>Design-time factory used by EF tools (dotnet ef migrations ...).</summary>
public class CatalogueDbContextFactory : IDesignTimeDbContextFactory<CatalogueDbContext>
{
    public CatalogueDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogueDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=CatalogueDb;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsAssembly(typeof(CatalogueDbContext).Assembly.FullName));

        return new CatalogueDbContext(optionsBuilder.Options);
    }
}
