using Catalogue.Core.Abstractions;
using Catalogue.Core.Interfaces;
using Catalogue.Core.Models;
using Dacpac.Management.Models;

namespace Dacpac.Management.Services;

/// <summary>
/// Imports full DACPAC schema (tables, columns, indexes, FKs, constraints,
/// views, stored procedures, functions, triggers) into the Catalogue database.
/// </summary>
public class DacpacSchemaImportService
{
    private readonly ICatalogueRepository _repository;
    private readonly DacpacExtractorService _extractor;
    private readonly ModelXmlParserService _parser;
    private readonly PrimaryKeyEnricher _pkEnricher;
    private readonly IGenerationLogger _logger;

    public DacpacSchemaImportService(
        ICatalogueRepository repository,
        DacpacExtractorService extractor,
        ModelXmlParserService parser,
        PrimaryKeyEnricher pkEnricher,
        IGenerationLogger logger)
    {
        _repository = repository;
        _extractor = extractor;
        _parser = parser;
        _pkEnricher = pkEnricher;
        _logger = logger;
    }

    /// <summary>
    /// Enumerates all .dacpac files in <paramref name="folderPath"/> and imports each one.
    /// Each file must follow the naming convention <c>&lt;server-name&gt;_&lt;database-name&gt;.dacpac</c>;
    /// the server name and database name are parsed from the file name by splitting on the first underscore.
    /// </summary>
    public async Task<IReadOnlyList<DacpacImportResult>> ImportDacpacFolderAsync(string folderPath)
    {
        var results = new List<DacpacImportResult>();

        if (!Directory.Exists(folderPath))
        {
            _logger.LogError($"DACPAC folder not found: {folderPath}");
            return results;
        }

        var dacpacFiles = Directory.GetFiles(folderPath, "*.dacpac", SearchOption.TopDirectoryOnly);

        if (dacpacFiles.Length == 0)
        {
            _logger.LogWarning($"No .dacpac files found in: {folderPath}");
            return results;
        }

        _logger.LogProgress($"Found {dacpacFiles.Length} DACPAC file(s) to import.");

        foreach (var dacpacPath in dacpacFiles)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(dacpacPath);
            var separatorIndex = fileNameWithoutExt.IndexOf('_');

            if (separatorIndex <= 0)
            {
                _logger.LogError(
                    $"Skipping '{Path.GetFileName(dacpacPath)}': file name does not match the required pattern <server-name>_<database-name>.dacpac");
                results.Add(new DacpacImportResult
                {
                    DatabaseName = fileNameWithoutExt,
                    ServerName   = string.Empty,
                    Success      = false,
                    Errors       = { "File name does not match the required pattern <server-name>_<database-name>.dacpac" }
                });
                continue;
            }

            var serverName   = fileNameWithoutExt[..separatorIndex];
            var databaseName = fileNameWithoutExt[(separatorIndex + 1)..];

            _logger.LogProgress($"Importing: {Path.GetFileName(dacpacPath)} → {serverName}/{databaseName}");

            try
            {
                await using var stream = File.OpenRead(dacpacPath);
                var result = await ImportSingleDacpacAsync(stream, serverName, databaseName);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to import {dacpacPath}: {ex.Message}");
                results.Add(new DacpacImportResult
                {
                    DatabaseName = databaseName,
                    ServerName   = serverName,
                    Success      = false,
                    Errors       = { ex.Message }
                });
            }
        }

        _logger.LogProgress($"Folder import complete. {results.Count(r => r.Success)}/{results.Count} succeeded.");
        return results;
    }

    /// <summary>
    /// Imports a single DACPAC stream into the Catalogue database.
    /// Uses upsert for SourceTable/SourceColumn records (preserving catalogue metadata),
    /// and delete-and-replace for schema objects (indexes, FKs, constraints, etc.)
    /// </summary>
    public async Task<DacpacImportResult> ImportSingleDacpacAsync(
        Stream dacpacStream, string serverName, string databaseName)
    {
        var result = new DacpacImportResult { ServerName = serverName, DatabaseName = databaseName };

        try
        {
            // ── 1. Extract and parse model XML ────────────────────────────────
            var modelXml = _extractor.ExtractModelXmlFromStream(dacpacStream, serverName, databaseName);
            if (string.IsNullOrWhiteSpace(modelXml))
            {
                result.Errors.Add("Could not extract model.xml from DACPAC.");
                return result;
            }

            var doc = _parser.PrepareDocument(modelXml, serverName, databaseName);
            if (doc is null)
            {
                result.Errors.Add("Failed to parse model XML document.");
                return result;
            }

            var tables    = _parser.ParseAllTables(doc, serverName, databaseName);
            var views     = _parser.ParseViews(doc, serverName, databaseName);
            var procs     = _parser.ParseStoredProcedures(doc, serverName, databaseName);
            var functions = _parser.ParseUserDefinedFunctions(doc, serverName, databaseName);
            var triggers  = _parser.ParseTriggers(doc, serverName, databaseName);

            _logger.LogProgress(
                $"Parsed: {tables.Count} tables, {views.Count} views, {procs.Count} procs, " +
                $"{functions.Count} functions, {triggers.Count} triggers");

            foreach (var t in tables)
                _pkEnricher.EnrichTableWithPrimaryKeys(t);

            // ── 2. Find or create Source ──────────────────────────────────────
            var sources = await _repository.GetSourcesAsync(includeInactive: true);
            var source = sources.FirstOrDefault(s =>
                string.Equals(s.ServerName, serverName, StringComparison.OrdinalIgnoreCase));

            if (source is null)
            {
                source = await _repository.AddSourceAsync(new Source { ServerName = serverName });
                _logger.LogProgress($"Created new Source: {serverName}");
            }

            // ── 3. Find or create SourceDatabase ─────────────────────────────
            var databases = await _repository.GetInScopeDatabasesAsync(source.SourceId, includeInactive: true);
            var dbInfo = databases.FirstOrDefault(d =>
                string.Equals(d.DatabaseName, databaseName, StringComparison.OrdinalIgnoreCase));

            int databaseId;
            if (dbInfo is null)
            {
                var newDb = await _repository.AddDatabaseAsync(new SourceDatabase
                {
                    SourceId = source.SourceId,
                    DatabaseName = databaseName
                });
                databaseId = newDb.DatabaseId;
                _logger.LogProgress($"Created new SourceDatabase: {databaseName}");
            }
            else
            {
                databaseId = dbInfo.DatabaseId;
            }

            // ── 4. Upsert SourceTables and enrich SourceColumn schema metadata
            var existingTables = await _repository.GetInScopeTablesAsync(databaseId: databaseId, includeInactive: true);
            var tableMap = existingTables.ToDictionary(
                t => $"{t.SchemaName}.{t.TableName}".ToUpperInvariant(),
                t => t.TableId);

            foreach (var tableDef in tables)
            {
                var tableKey = $"{tableDef.Schema}.{tableDef.TableName}".ToUpperInvariant();

                if (!tableMap.TryGetValue(tableKey, out var tableId))
                {
                    var newTable = await _repository.AddTableAsync(new SourceTable
                    {
                        DatabaseId = databaseId,
                        SchemaName = tableDef.Schema,
                        TableName = tableDef.TableName
                    });
                    tableId = newTable.TableId;
                    tableMap[tableKey] = tableId;
                }

                result.TablesProcessed++;

                // Upsert columns — update schema metadata on existing, create new
                var existingCols = await _repository.GetColumnsAsync(tableId: tableId, includeInactive: true);
                var colMap = existingCols.ToDictionary(
                    c => c.ColumnName.ToUpperInvariant(),
                    c => c.ColumnId);

                var toUpdate = tableDef.Columns
                    .Where(c => colMap.ContainsKey(c.Name.ToUpperInvariant()))
                    .ToList();

                if (toUpdate.Count > 0)
                {
                    var colLookup = toUpdate.ToDictionary(c => c.Name.ToUpperInvariant());
                    await _repository.BulkUpdateColumnsAsync(
                        toUpdate.Select(c => colMap[c.Name.ToUpperInvariant()]),
                        col =>
                        {
                            if (colLookup.TryGetValue(col.ColumnName.ToUpperInvariant(), out var def))
                                ApplySchemaMetadata(col, def);
                        });
                    result.ColumnsUpdated += toUpdate.Count;
                }

                foreach (var colDef in tableDef.Columns.Where(c => !colMap.ContainsKey(c.Name.ToUpperInvariant())))
                {
                    var newCol = new SourceColumn
                    {
                        TableId = tableId,
                        ColumnName = colDef.Name,
                        SortOrder = tableDef.Columns.IndexOf(colDef)
                    };
                    ApplySchemaMetadata(newCol, colDef);
                    await _repository.AddColumnAsync(newCol);
                    result.ColumnsCreated++;
                }
            }

            // ── 5. Delete existing schema objects (delete-and-replace) ────────
            _logger.LogProgress("Clearing existing schema objects...");
            await _repository.DeleteSchemaForDatabaseAsync(databaseId);

            // ── 6. Re-insert table-level schema objects ───────────────────────
            foreach (var tableDef in tables)
            {
                var tableKey = $"{tableDef.Schema}.{tableDef.TableName}".ToUpperInvariant();
                if (!tableMap.TryGetValue(tableKey, out var tableId)) continue;

                var indexes = tableDef.Indexes.Select(ix => new SourceIndex
                {
                    TableId = tableId,
                    Name = ix.Name,
                    IsUnique = ix.IsUnique,
                    IsClustered = ix.IsClustered,
                    IsPrimaryKeyIndex = ix.IsPrimaryKeyIndex,
                    FilterDefinition = ix.FilterDefinition,
                    Columns = ix.Columns
                        .Select(colName => new SourceIndexColumn
                        {
                            ColumnName = colName,
                            SortOrder = ix.ColumnSortOrder.TryGetValue(colName, out var isAsc)
                                ? (isAsc ? "ASC" : "DESC") : "ASC",
                            IsIncludedColumn = false
                        })
                        .Concat(ix.IncludedColumns.Select(colName => new SourceIndexColumn
                        {
                            ColumnName = colName,
                            SortOrder = "ASC",
                            IsIncludedColumn = true
                        }))
                        .ToList()
                }).ToList();

                var foreignKeys = tableDef.ForeignKeys.Select(fk => new SourceForeignKey
                {
                    TableId = tableId,
                    Name = fk.Name,
                    ToSchema = fk.ToSchema,
                    ToTable = fk.ToTable,
                    OnDeleteCascade = fk.OnDeleteCascade,
                    OnUpdateCascade = fk.OnUpdateCascade,
                    Cardinality = fk.Cardinality.ToString(),
                    Columns = fk.FromColumns
                        .Zip(fk.ToColumns, (from, to) => (from, to))
                        .Select((pair, ord) => new SourceForeignKeyColumn
                        {
                            FromColumn = pair.from,
                            ToColumn = pair.to,
                            Ordinal = ord
                        })
                        .ToList()
                }).ToList();

                var checks = tableDef.CheckConstraints.Select(c => new SourceCheckConstraint
                {
                    TableId = tableId,
                    Name = c.Name,
                    Expression = c.Expression
                }).ToList();

                var uniques = tableDef.UniqueConstraints.Select(uc => new SourceUniqueConstraint
                {
                    TableId = tableId,
                    Name = uc.Name,
                    IsClustered = uc.IsClustered,
                    Columns = uc.Columns
                        .Select(colName => new SourceUniqueConstraintColumn { ColumnName = colName })
                        .ToList()
                }).ToList();

                var tableTriggers = triggers
                    .Where(t =>
                        string.Equals(t.ParentSchema, tableDef.Schema, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(t.ParentTable, tableDef.TableName, StringComparison.OrdinalIgnoreCase))
                    .Select(t => new SourceTrigger
                    {
                        TableId = tableId,
                        SchemaName = t.Schema,
                        TriggerName = t.Name,
                        SqlBody = t.SqlBody
                    }).ToList();

                await _repository.BulkInsertTableSchemaAsync(
                    tableId, indexes, foreignKeys, checks, uniques, tableTriggers);

                result.IndexesImported += indexes.Count;
                result.ForeignKeysImported += foreignKeys.Count;
                result.TriggersImported += tableTriggers.Count;
            }

            // ── 7. Insert views ───────────────────────────────────────────────
            var sourceViews = views.Select(v => new SourceView
            {
                DatabaseId = databaseId,
                SchemaName = v.Schema,
                ViewName = v.ViewName,
                SqlBody = v.SqlBody,
                HasStandardAuditColumns = v.HasStandardAuditColumns,
                Columns = v.Columns.Select((c, i) => new SourceViewColumn
                {
                    ColumnName = c.Name,
                    SqlType = c.SqlType,
                    IsNullable = c.IsNullable,
                    MaxLength = c.MaxLength,
                    Precision = c.Precision,
                    Scale = c.Scale,
                    OrdinalPosition = i
                }).ToList()
            }).ToList();

            await _repository.BulkInsertViewsAsync(sourceViews);
            result.ViewsImported = sourceViews.Count;

            // ── 8. Insert stored procedures ───────────────────────────────────
            var sourceProcs = procs.Select(p => new SourceStoredProcedure
            {
                DatabaseId = databaseId,
                SchemaName = p.Schema,
                ProcedureName = p.Name,
                SqlBody = p.SqlBody,
                Parameters = p.Parameters.Select(param => new SourceStoredProcedureParameter
                {
                    Name = param.Name,
                    SqlType = param.SqlType,
                    IsOutput = param.IsOutput,
                    DefaultValue = param.DefaultValue
                }).ToList()
            }).ToList();

            await _repository.BulkInsertStoredProceduresAsync(sourceProcs);
            result.StoredProceduresImported = sourceProcs.Count;

            // ── 9. Insert functions ───────────────────────────────────────────
            var sourceFunctions = functions.Select(f => new SourceFunction
            {
                DatabaseId = databaseId,
                SchemaName = f.Schema,
                FunctionName = f.FunctionName,
                FunctionType = f.Type.ToString(),
                ReturnType = f.ReturnType,
                SqlBody = f.SqlBody
            }).ToList();

            await _repository.BulkInsertFunctionsAsync(sourceFunctions);
            result.FunctionsImported = sourceFunctions.Count;

            result.Success = true;
            _logger.LogProgress(
                $"Import complete for {serverName}/{databaseName}: " +
                $"{result.TablesProcessed} tables, " +
                $"{result.ColumnsUpdated} cols updated / {result.ColumnsCreated} cols created, " +
                $"{result.ViewsImported} views, {result.StoredProceduresImported} procs, " +
                $"{result.FunctionsImported} functions, {result.IndexesImported} indexes, " +
                $"{result.TriggersImported} triggers");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            _logger.LogError($"Import failed for {serverName}/{databaseName}: {ex.Message}");
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ApplySchemaMetadata(SourceColumn col, ColumnDefinition def)
    {
        col.SqlType             = def.SqlType;
        col.IsNullable          = def.IsNullable;
        col.MaxLength           = def.MaxLength;
        col.IsIdentity          = def.IsIdentity;
        col.IsPrimaryKey        = def.IsPrimaryKey;
        col.Precision           = def.Precision;
        col.Scale               = def.Scale;
        col.DefaultValue        = def.DefaultValue;
        col.IsComputed          = def.IsComputed;
        col.IsComputedPersisted = def.IsComputedPersisted;
        col.ComputedExpression  = def.ComputedExpression;
        col.IsRowVersion        = def.IsRowVersion;
        col.IsConcurrencyToken  = def.IsConcurrencyToken;
        col.Collation           = def.Collation;
        col.Description         = def.Description;
    }
}
