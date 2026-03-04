using DataManager.Core.Interfaces;
using DataManager.Infrastructure.Dacpac;
using DataManager.Infrastructure.Data;
using DataManager.Infrastructure.Generation;
using DataManager.Infrastructure.Import;
using DataManager.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataManager.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<DataManagerDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DataManagerDb"),
                sql => sql.MigrationsAssembly(typeof(DataManagerDbContext).Assembly.FullName)));

        // IDbContextFactory is used by EfDataManagerRepository so that each repository
        // method gets its own short-lived DbContext, preventing concurrent-operation
        // errors in Blazor Server where the scoped DbContext would otherwise be shared
        // across all components in the same SignalR circuit.
        services.AddDbContextFactory<DataManagerDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DataManagerDb"),
                sql => sql.MigrationsAssembly(typeof(DataManagerDbContext).Assembly.FullName)),
            ServiceLifetime.Scoped);

        services.AddScoped<IDataManagerRepository, EfDataManagerRepository>();

        // Import services
        services.AddScoped<DataManagerImportService>();
        services.AddSingleton<ExcelImportService>();

        // DACPAC parsing services
        services.AddTransient<DacpacExtractorService>();
        services.AddTransient<ModelXmlParserService>();
        services.AddTransient<PrimaryKeyEnricher>();
        services.AddScoped<DacpacSchemaImportService>();
        services.AddScoped<DataManagerDbSchemaDataSource>();

        // EF entity generation services
        services.AddTransient<ExcelReaderService>();
        services.AddTransient<EntityClassGenerator>();
        services.AddTransient<EntityConfigurationGenerator>();
        services.AddTransient<FileWriterService>();
        services.AddTransient<DbContextGenerator>();
        services.AddTransient<GenerationOrchestrator>();
        services.AddTransient<DataManagerGenerationOrchestrator>();
        services.AddTransient<SummaryDisplayService>();

        return services;
    }
}
