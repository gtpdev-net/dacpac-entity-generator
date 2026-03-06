using DataManager.Core.Models.Dacpac;

namespace DataManager.Core.Abstractions;

/// <summary>
/// Abstraction over the source of schema data used by the generation pipeline.
/// Implementations may read from DACPAC + Excel files or directly from DataManagerDb.
/// </summary>
public interface ISchemaDataSource
{
    /// <summary>
    /// Returns all tables (with their columns) that should have entity classes generated.
    /// Filtering (e.g., IsSelectedForLoad, PersistenceType) is the responsibility of the implementation.
    /// </summary>
    Task<List<TableDefinition>> GetTablesForGenerationAsync();

    /// <summary>
    /// Returns a discovery report describing schema elements (stored procs, triggers, etc.)
    /// that may require manual attention.
    /// </summary>
    Task<ElementDiscoveryReport> GetDiscoveryReportAsync();
}
