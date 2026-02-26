using Catalogue.Core.DTOs;
using System.IO.Compression;
using System.Xml.Linq;

namespace Catalogue.Infrastructure.Import;

/// <summary>
/// Parses a .dacpac file (which is a ZIP archive) extracting table/column
/// metadata from model.xml without requiring Windows-only SqlServer.Dac.
/// </summary>
public class DacpacParserService
{
    private static readonly XNamespace Dsql = "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02";

    public IReadOnlyList<ImportPreviewRow> Parse(Stream dacpacStream, string serverName, string databaseName)
    {
        var results = new List<ImportPreviewRow>();

        using var zip = new ZipArchive(dacpacStream, ZipArchiveMode.Read, leaveOpen: true);
        var modelEntry = zip.GetEntry("model.xml") ?? zip.GetEntry("Model/model.xml");
        if (modelEntry is null)
            throw new InvalidDataException("model.xml not found in DACPAC archive.");

        using var xmlStream = modelEntry.Open();
        var doc = XDocument.Load(xmlStream);

        // Columns are <Element Type="SqlSimpleColumn" ...>
        // Parent table: the containing element of type SqlTable
        var elements = doc.Descendants(Dsql + "Element").ToList();

        // Build a lookup: table name → schema, table
        var tables = elements
            .Where(e => e.Attribute("Type")?.Value is "SqlTable")
            .ToList();

        var tableMap = new Dictionary<string, (string Schema, string Table)>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tables)
        {
            var name = t.Attribute("Name")?.Value ?? string.Empty;
            var parts = SplitName(name);
            if (parts.Length >= 2)
                tableMap[name] = (Unquote(parts[^2]), Unquote(parts[^1]));
        }

        // Columns
        var columns = elements.Where(e => e.Attribute("Type")?.Value is "SqlSimpleColumn");
        int sortOrder = 10;
        foreach (var col in columns)
        {
            var colName = col.Attribute("Name")?.Value ?? string.Empty;
            var parts = SplitName(colName);
            if (parts.Length < 3) continue;

            var tableKey = string.Join(".", parts[..^1]);
            if (!tableMap.TryGetValue(tableKey, out var tbl)) continue;

            results.Add(new ImportPreviewRow
            {
                ServerName   = serverName,
                DatabaseName = databaseName,
                SchemaName   = tbl.Schema,
                TableName    = tbl.Table,
                ColumnName   = Unquote(parts[^1]),
                PersistenceType = 'R'
            });
            sortOrder += 10;
        }

        return results;
    }

    private static string[] SplitName(string name)
        => name.Split('.', StringSplitOptions.RemoveEmptyEntries);

    private static string Unquote(string s)
        => s.TrimStart('[').TrimEnd(']').Trim('"');
}
