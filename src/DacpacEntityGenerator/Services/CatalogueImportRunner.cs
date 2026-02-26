using Catalogue.Core.DTOs;
using Catalogue.Infrastructure.Data;
using Catalogue.Infrastructure.Import;
using DacpacEntityGenerator.Utilities;
using Microsoft.EntityFrameworkCore;

namespace DacpacEntityGenerator.Services;

/// <summary>
/// Orchestrates a full Excel → Catalogue DB import run.
/// Builds its own <see cref="CatalogueDbContext"/> from a connection string,
/// applies any pending EF migrations, then delegates to the Catalogue's own
/// <see cref="ExcelImportService"/> and <see cref="CatalogueImportService"/>.
/// </summary>
public class CatalogueImportRunner
{
    private readonly string _connectionString;

    public CatalogueImportRunner(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("A connection string is required for Catalogue DB import.", nameof(connectionString));

        _connectionString = connectionString;
    }

    /// <summary>
    /// Parses <paramref name="excelFilePath"/> and persists ALL rows (no intent filter)
    /// into the Catalogue database.
    /// </summary>
    /// <param name="excelFilePath">Absolute or relative path to the .xlsx workbook.</param>
    /// <param name="strategy">Conflict strategy (default: <see cref="ImportConflictStrategy.FullSync"/>).</param>
    /// <param name="dryRun">When <c>true</c>, no changes are written; only counts are returned.</param>
    /// <returns>Summary of the import operation.</returns>
    public async Task<ImportResultDto> RunAsync(
        string excelFilePath,
        ImportConflictStrategy strategy = ImportConflictStrategy.FullSync,
        bool dryRun = false)
    {
        // ── 1. Build DbContext ─────────────────────────────────────────────
        var options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        await using var db = new CatalogueDbContext(options);

        // ── 2. Apply any pending EF migrations ────────────────────────────
        ConsoleLogger.LogInfo("Applying pending EF migrations (if any)...");
        await db.Database.MigrateAsync();
        ConsoleLogger.LogProgress("Database schema is up to date.");

        // ── 3. Parse Excel using Catalogue's own parser ───────────────────
        ConsoleLogger.LogInfo($"Parsing Excel file: {Path.GetFileName(excelFilePath)}");
        var excelService = new ExcelImportService();

        await using var stream = File.OpenRead(excelFilePath);
        var (rows, unrecognised) = excelService.Parse(stream);

        if (unrecognised.Count > 0)
        {
            ConsoleLogger.LogWarning($"Unrecognised Excel headers (ignored): {string.Join(", ", unrecognised)}");
        }

        ConsoleLogger.LogProgress($"Parsed {rows.Count} rows from Excel.");

        var warnings = rows.Count(r => !string.IsNullOrEmpty(r.Warning));
        if (warnings > 0)
        {
            ConsoleLogger.LogWarning($"{warnings} rows have warnings (empty column name) and will be skipped.");
        }

        // ── 4. Persist via Catalogue import service ────────────────────────
        var importService = new CatalogueImportService(db);

        ConsoleLogger.LogInfo($"Importing to Catalogue DB [{(dryRun ? "DRY RUN" : "LIVE")}] " +
                              $"with strategy: {strategy}...");

        var result = await importService.ImportAsync(rows, strategy, dryRun);

        return result;
    }
}
