using Catalogue.Core.Interfaces;
using Catalogue.Infrastructure.Dacpac;
using Catalogue.Infrastructure.Data;
using Catalogue.Infrastructure.Generation;
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
        services.AddSingleton<ExcelImportService>();

        // DACPAC parsing services
        services.AddTransient<DacpacExtractorService>();
        services.AddTransient<ModelXmlParserService>();
        services.AddTransient<PrimaryKeyEnricher>();
        services.AddScoped<DacpacSchemaImportService>();
        services.AddScoped<CatalogueDbSchemaDataSource>();

        // EF entity generation services
        services.AddTransient<ExcelReaderService>();
        services.AddTransient<EntityClassGenerator>();
        services.AddTransient<EntityConfigurationGenerator>();
        services.AddTransient<FileWriterService>();
        services.AddTransient<DbContextGenerator>();
        services.AddTransient<GenerationOrchestrator>();
        services.AddTransient<CatalogueGenerationOrchestrator>();
        services.AddTransient<SummaryDisplayService>();

        return services;
    }
}
