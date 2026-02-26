using Catalogue.Core.DTOs;
using ClosedXML.Excel;

namespace Catalogue.Infrastructure.Import;

/// <summary>
/// Imports catalogue data from an Excel workbook (.xlsx) using ClosedXML.
/// Reads from the sheet named "All". Maps known headers automatically.
/// </summary>
public class ExcelImportService
{
    private static readonly Dictionary<string, string> KnownHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Server"]                = "ServerName",
            ["Server Name"]           = "ServerName",
            ["Database"]              = "DatabaseName",
            ["Database Name"]         = "DatabaseName",
            ["Schema"]                = "SchemaName",
            ["Table"]                 = "TableName",
            ["Table Name"]            = "TableName",
            ["Column"]                = "ColumnName",
            ["Column Name"]           = "ColumnName",
            ["PersistenceType"]       = "PersistenceType",
            ["Persistence Type"]      = "PersistenceType",
            ["Type"]                  = "PersistenceType",
            ["IsInDaoAnalysis"]       = "IsInDaoAnalysis",
            ["DAO"]                   = "IsInDaoAnalysis",
            ["In DAO Analysis"]       = "IsInDaoAnalysis",
            ["IsAddedByApi"]          = "IsAddedByApi",
            ["API"]                   = "IsAddedByApi",
            ["Added By API"]          = "IsAddedByApi",
            ["IsSelectedForLoad"]     = "IsSelectedForLoad",
            ["Selected For Load"]     = "IsSelectedForLoad",
            ["Generate SQL INSERTS"]  = "IsSelectedForLoad",
            ["For Load"]              = "IsSelectedForLoad",
            ["Generate"]              = "IsSelectedForLoad",
            ["Table in DAO Analysis"] = "IsInDaoAnalysis",
            ["DEV Persistence Type"]  = "DevPersistenceType",
            ["Number of Records"]     = "NumberOfRecords",
        };

    public (IReadOnlyList<ImportPreviewRow> Rows, IReadOnlyList<string> UnrecognisedHeaders) Parse(Stream stream)
    {
        var rows = new List<ImportPreviewRow>();
        var unrecognised = new List<string>();

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault(w =>
               w.Name.Equals("All", StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.First();

        var firstRow = ws.FirstRowUsed();
        if (firstRow is null) return (rows, unrecognised);

        // Map column index → field name
        var colMap = new Dictionary<int, string>();
        foreach (var cell in firstRow.CellsUsed())
        {
            var header = cell.GetString().Trim();
            if (KnownHeaders.TryGetValue(header, out var field))
                colMap[cell.Address.ColumnNumber] = field;
            else
                unrecognised.Add(header);
        }

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var pr = new ImportPreviewRow();
            char? devPersistenceType = null;
            foreach (var (colIdx, field) in colMap)
            {
                var value = row.Cell(colIdx).GetString().Trim();
                switch (field)
                {
                    case "ServerName":        pr.ServerName        = value; break;
                    case "DatabaseName":      pr.DatabaseName      = value; break;
                    case "SchemaName":        pr.SchemaName        = value; break;
                    case "TableName":         pr.TableName         = value; break;
                    case "ColumnName":        pr.ColumnName        = value; break;
                    case "PersistenceType":   pr.PersistenceType   = string.IsNullOrEmpty(value) ? 'R' : char.ToUpperInvariant(value[0]); break;
                    case "DevPersistenceType": devPersistenceType  = string.IsNullOrEmpty(value) ? (char?)null : char.ToUpperInvariant(value[0]); break;
                    case "IsInDaoAnalysis":   pr.IsInDaoAnalysis   = ParseBool(value); break;
                    case "IsAddedByApi":      pr.IsAddedByApi      = ParseBool(value); break;
                    case "IsSelectedForLoad": pr.IsSelectedForLoad = ParseBool(value); break;
                    case "NumberOfRecords":   pr.NumberOfRecords   = ParseLong(value); break;
                }
            }

            // DEV Persistence Type overrides PersistenceType when they differ
            if (devPersistenceType.HasValue && devPersistenceType.Value != pr.PersistenceType)
                pr.PersistenceType = devPersistenceType.Value;

            if (string.IsNullOrEmpty(pr.ColumnName))
            {
                pr.Warning = "Empty column name — row skipped.";
            }

            rows.Add(pr);
        }

        return (rows, unrecognised);
    }

    private static bool ParseBool(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        return s.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "1" or "YES" or "Y" => true,
            _ => false
        };
    }

    private static long? ParseLong(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        // Strip formatting characters (commas, spaces)
        var cleaned = s.Trim().Replace(",", "").Replace(" ", "");
        return long.TryParse(cleaned, out var result) ? result : null;
    }
}
