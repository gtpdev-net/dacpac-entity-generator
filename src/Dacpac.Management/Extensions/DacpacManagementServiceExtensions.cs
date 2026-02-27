using Dacpac.Management.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Dacpac.Management.Extensions;

public static class DacpacManagementServiceExtensions
{
    /// <summary>
    /// Registers all Dacpac.Management services.
    /// Callers must separately register an <c>IGenerationLogger</c> implementation.
    /// </summary>
    public static IServiceCollection AddDacpacManagement(this IServiceCollection services)
    {
        services.AddTransient<DacpacExtractorService>();
        services.AddTransient<ModelXmlParserService>();
        services.AddTransient<PrimaryKeyEnricher>();
        services.AddScoped<DacpacSchemaImportService>();

        // CatalogueDb-sourced generation data source (Scoped — caller sets DatabaseId per request)
        services.AddScoped<CatalogueDbSchemaDataSource>();

        return services;
    }
}
