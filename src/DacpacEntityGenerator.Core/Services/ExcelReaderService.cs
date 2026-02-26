using ClosedXML.Excel;
using DacpacEntityGenerator.Core.Abstractions;
using DacpacEntityGenerator.Core.Models;
using DacpacEntityGenerator.Core.Utilities;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DacpacEntityGenerator.Core.Services;

public class ExcelReaderService
{
    private readonly IGenerationLogger _logger;

    public ExcelReaderService(IGenerationLogger logger)
    {
        _logger = logger;
    }

    public string? FindExcelFile(string inputDirectory)
    {
        _logger.LogInfo($"Scanning for Excel files in {inputDirectory}");
        
        if (!Directory.Exists(inputDirectory))
        {
            _logger.LogError($"Input directory does not exist: {inputDirectory}");
            return null;
        }

        var excelFiles = Directory.GetFiles(inputDirectory, "*.xlsx");
        
        if (excelFiles.Length == 0)
        {
            _logger.LogWarning("No Excel file found in ./input/ folder");
            return null;
        }

        var selectedFile = excelFiles[0];
        _logger.LogProgress($"Found Excel file: {Path.GetFileName(selectedFile)}");
        
        if (excelFiles.Length > 1)
        {
            _logger.LogWarning($"Multiple Excel files found, using: {Path.GetFileName(selectedFile)}");
        }

        return selectedFile;
    }

    public List<ExcelRow> ReadAndFilterExcel(string filePath)
    {
        var allRows = new List<ExcelRow>();
        
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            
            // Find header row and column indices
            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                _logger.LogError("Excel file is empty");
                return new List<ExcelRow>();
            }

            var columnIndices = GetColumnIndices(headerRow);
            if (!ValidateColumns(columnIndices))
            {
                return new List<ExcelRow>();
            }

            // Read data rows
            var rows = worksheet.RowsUsed().Skip(1); // Skip header
            
            foreach (var row in rows)
            {
                try
                {
                    var excelRow = new ExcelRow
                    {
                        Server = row.Cell(columnIndices["Server"]).GetString().Trim(),
                        Database = row.Cell(columnIndices["Database"]).GetString().Trim(),
                        Schema = row.Cell(columnIndices["Schema"]).GetString().Trim(),
                        Table = row.Cell(columnIndices["Table"]).GetString().Trim(),
                        Column = row.Cell(columnIndices["Column"]).GetString().Trim(),
                        TableInDaoAnalysis = ParseBoolean(row.Cell(columnIndices["Table in DAO Analysis"])),
                        AddedByAPI = ParseBoolean(row.Cell(columnIndices["Added by API"])),
                        PersistenceType = row.Cell(columnIndices["Persistence Type"]).GetString().Trim(),
                        DevPersistenceType = row.Cell(columnIndices["DEV Persistence Type"]).GetString().Trim(),
                        Generate = ParseBoolean(row.Cell(columnIndices["Generate"]))
                    };

                    allRows.Add(excelRow);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to parse row {row.RowNumber()}: {ex.Message}");
                }
            }

            _logger.LogInfo($"Loaded {allRows.Count} rows from Excel");

            // Apply filtering
            var filteredRows = allRows
                .Where(r => (r.Generate && r.DevPersistenceType.Equals("R", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _logger.LogProgress($"Filtered to {filteredRows.Count} rows (Table in DAO Analysis = TRUE, Added by API = TRUE, Persistence Type = 'R', DEV Persistence Type = 'R')");

            // Group and log summary
            var serverCount = filteredRows.Select(r => r.Server).Distinct().Count();
            var dbCount = filteredRows.Select(r => new { r.Server, r.Database }).Distinct().Count();
            var tableCount = filteredRows.Select(r => new { r.Server, r.Database, r.Schema, r.Table }).Distinct().Count();
            
            _logger.LogInfo($"Grouped into {serverCount} servers, {dbCount} databases, {tableCount} tables");

            return filteredRows;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to read Excel file: {ex.Message}");
            return new List<ExcelRow>();
        }
    }

    private Dictionary<string, int> GetColumnIndices(IXLRow headerRow)
    {
        var indices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var cell in headerRow.CellsUsed())
        {
            var columnName = cell.GetString().Trim();
            indices[columnName] = cell.Address.ColumnNumber;
        }

        return indices;
    }

    private bool ValidateColumns(Dictionary<string, int> columnIndices)
    {
        var requiredColumns = new[] 
        { 
            "Server", "Database", "Schema", "Table", "Column", 
            "Table in DAO Analysis", "Added by API", "Persistence Type", "DEV Persistence Type", "Generate"
        };

        var missingColumns = requiredColumns
            .Where(col => !columnIndices.ContainsKey(col))
            .ToList();

        if (missingColumns.Any())
        {
            _logger.LogError($"Excel file is missing required columns: {string.Join(", ", missingColumns)}");
            _logger.LogError("Expected columns: Server, Database, Schema, Table, Column, Table in DAO Analysis, Added by API, Persistence Type, DEV Persistence Type, Generate");
            return false;
        }

        return true;
    }

    private bool ParseBoolean(IXLCell cell)
    {
        try
        {
            // Try different boolean representations
            var value = cell.GetString().Trim();
            
            if (bool.TryParse(value, out var boolResult))
                return boolResult;
            
            if (int.TryParse(value, out var intResult))
                return intResult != 0;
            
            return value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public Dictionary<string, Dictionary<string, List<ExcelRow>>> GroupByServerAndDatabase(List<ExcelRow> rows)
    {
        return rows
            .GroupBy(r => r.Server)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Database)
                      .ToDictionary(dbg => dbg.Key, dbg => dbg.ToList())
            );
    }
}
