using Catalogue.Core.Interfaces;
using Catalogue.Infrastructure.Data;
using Catalogue.Infrastructure.Import;
using Catalogue.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogue.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CatalogueDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("CatalogueDb"),
                sql => sql.MigrationsAssembly(typeof(CatalogueDbContext).Assembly.FullName)));

        services.AddScoped<ICatalogueRepository, EfCatalogueRepository>();

        // Import services
        services.AddScoped<CatalogueImportService>();
        services.AddSingleton<DacpacParserService>();
        services.AddSingleton<ExcelImportService>();

        return services;
    }
}
