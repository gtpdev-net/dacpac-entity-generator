using DacpacEntityGenerator.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DacpacEntityGenerator.Core.Extensions;

public static class GenerationServiceExtensions
{
    /// <summary>
    /// Registers all DacpacEntityGenerator generation services (code gen only).
    /// DACPAC parsing services are registered via AddDacpacManagement().
    /// Callers must separately register an <c>IGenerationLogger</c> implementation.
    /// </summary>
    public static IServiceCollection AddGenerationServices(this IServiceCollection services)
    {
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
