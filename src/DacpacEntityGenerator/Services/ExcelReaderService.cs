using ClosedXML.Excel;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class ExcelReaderService
{
    public string? FindExcelFile(string inputDirectory)
    {
        ConsoleLogger.LogInfo($"Scanning for Excel files in {inputDirectory}");
        
        if (!Directory.Exists(inputDirectory))
        {
            ConsoleLogger.LogError($"Input directory does not exist: {inputDirectory}");
            return null;
        }

        var excelFiles = Directory.GetFiles(inputDirectory, "*.xlsx");
        
        if (excelFiles.Length == 0)
        {
            ConsoleLogger.LogWarning("No Excel file found in ./input/ folder");
            return null;
        }

        var selectedFile = excelFiles[0];
        ConsoleLogger.LogProgress($"Found Excel file: {Path.GetFileName(selectedFile)}");
        
        if (excelFiles.Length > 1)
        {
            ConsoleLogger.LogWarning($"Multiple Excel files found, using: {Path.GetFileName(selectedFile)}");
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
                ConsoleLogger.LogError("Excel file is empty");
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
                        PersistenceType = row.Cell(columnIndices["Persistence Type"]).GetString().Trim(),
                        AddedByAPI = ParseBoolean(row.Cell(columnIndices["Added by API"]))
                    };

                    allRows.Add(excelRow);
                }
                catch (Exception ex)
                {
                    ConsoleLogger.LogWarning($"Failed to parse row {row.RowNumber()}: {ex.Message}");
                }
            }

            ConsoleLogger.LogInfo($"Loaded {allRows.Count} rows from Excel");

            // Apply filtering
            var filteredRows = allRows
                .Where(r => (r.TableInDaoAnalysis || r.AddedByAPI) &&
                           r.PersistenceType.Equals("R", StringComparison.OrdinalIgnoreCase))
                .ToList();

            ConsoleLogger.LogProgress($"Filtered to {filteredRows.Count} rows (Table in DAO Analysis = TRUE, Persistence Type = 'R')");

            // Group and log summary
            var serverCount = filteredRows.Select(r => r.Server).Distinct().Count();
            var dbCount = filteredRows.Select(r => new { r.Server, r.Database }).Distinct().Count();
            var tableCount = filteredRows.Select(r => new { r.Server, r.Database, r.Schema, r.Table }).Distinct().Count();
            
            ConsoleLogger.LogInfo($"Grouped into {serverCount} servers, {dbCount} databases, {tableCount} tables");

            return filteredRows;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"Failed to read Excel file: {ex.Message}");
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
            "Table in DAO Analysis", "Persistence Type" 
        };

        var missingColumns = requiredColumns
            .Where(col => !columnIndices.ContainsKey(col))
            .ToList();

        if (missingColumns.Any())
        {
            ConsoleLogger.LogError($"Excel file is missing required columns: {string.Join(", ", missingColumns)}");
            ConsoleLogger.LogError("Expected columns: Server, Database, Schema, Table, Column, Table in DAO Analysis, Persistence Type");
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
