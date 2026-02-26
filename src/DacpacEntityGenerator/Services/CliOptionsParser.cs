using Catalogue.Infrastructure.Import;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;
using Microsoft.Extensions.Configuration;

namespace DacpacEntityGenerator.Services;

/// <summary>
/// Parses raw CLI arguments into a strongly-typed <see cref="CliOptions"/> instance.
/// </summary>
public static class CliOptionsParser
{
    /// <summary>
    /// Parses <paramref name="args"/> and returns a populated <see cref="CliOptions"/>.
    /// Falls back to <c>appsettings.json</c> for the connection string when
    /// <c>--import-to-db</c> is present but no <c>--connection-string</c> was supplied.
    /// </summary>
    public static CliOptions Parse(string[] args)
    {
        bool importToDb = args.Contains("--import-to-db");
        bool dryRun     = args.Contains("--dry-run");

        string? connectionString = null;
        var connIdx = Array.IndexOf(args, "--connection-string");
        if (connIdx >= 0 && connIdx < args.Length - 1)
            connectionString = args[connIdx + 1];

        if (importToDb && string.IsNullOrEmpty(connectionString))
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .Build();
            connectionString = config.GetConnectionString("CatalogueDb");
        }

        var strategy = ImportConflictStrategy.FullSync;
        var stratIdx = Array.IndexOf(args, "--conflict-strategy");
        if (stratIdx >= 0 && stratIdx < args.Length - 1)
        {
            if (Enum.TryParse<ImportConflictStrategy>(args[stratIdx + 1], ignoreCase: true, out var parsed))
                strategy = parsed;
            else
                ConsoleLogger.LogWarning($"Unknown conflict strategy '{args[stratIdx + 1]}', defaulting to FullSync.");
        }

        return new CliOptions
        {
            ImportToDb       = importToDb,
            DryRun           = dryRun,
            ConnectionString = connectionString,
            Strategy         = strategy,
        };
    }
}
