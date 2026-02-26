using Catalogue.Infrastructure.Import;

namespace DacpacEntityGenerator.Models;

/// <summary>
/// Represents parsed CLI arguments for a single invocation of the generator.
/// </summary>
public class CliOptions
{
    /// <summary>Whether the --import-to-db flag was present.</summary>
    public bool ImportToDb { get; init; }

    /// <summary>Whether the --dry-run flag was present.</summary>
    public bool DryRun { get; init; }

    /// <summary>Connection string resolved from --connection-string or appsettings.json.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>Conflict strategy resolved from --conflict-strategy (default: FullSync).</summary>
    public ImportConflictStrategy Strategy { get; init; } = ImportConflictStrategy.FullSync;
}
