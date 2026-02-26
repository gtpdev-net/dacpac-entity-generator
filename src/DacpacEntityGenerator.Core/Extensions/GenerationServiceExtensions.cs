using DacpacEntityGenerator.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DacpacEntityGenerator.Core.Extensions;

public static class GenerationServiceExtensions
{
    /// <summary>
    /// Registers all DacpacEntityGenerator generation services.
    /// Callers must separately register an <c>IGenerationLogger</c> implementation.
    /// </summary>
    public static IServiceCollection AddGenerationServices(this IServiceCollection services)
    {
        services.AddTransient<ExcelReaderService>();
        services.AddTransient<DacpacExtractorService>();
        services.AddTransient<ModelXmlParserService>();
        services.AddTransient<PrimaryKeyEnricher>();
        services.AddTransient<EntityClassGenerator>();
        services.AddTransient<FileWriterService>();
        services.AddTransient<ReportWriterService>();
        services.AddTransient<DbContextGenerator>();
        services.AddTransient<GenerationOrchestrator>();
        services.AddTransient<SummaryDisplayService>();

        return services;
    }
}
