using System.Text;
using DacpacEntityGenerator.Models;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class FileWriterService
{
    public bool WriteEntityFile(
        string outputDirectory,
        string server,
        string database,
        string schema,
        string tableName,
        string entityClassCode)
    {
        try
        {
            // Create output directory structure: ./output/{Server}/{Database}/
            var serverDir = Path.Combine(outputDirectory, server);
            var databaseDir = Path.Combine(serverDir, database);
            
            Directory.CreateDirectory(databaseDir);

            // Generate filename: {TableName}.cs (PascalCase)
            var className = NameConverter.ToPascalCase(tableName);
            var fileName = $"{className}.cs";
            var filePath = Path.Combine(databaseDir, fileName);

            // Write file with UTF-8 encoding
            File.WriteAllText(filePath, entityClassCode, Encoding.UTF8);

            // Get relative path for logging
            var relativePath = Path.GetRelativePath(outputDirectory, filePath);
            ConsoleLogger.LogProgress($"[{server}].[{database}].[{schema}].[{tableName}] - Generated entity: ./output/{relativePath.Replace('\\', '/')}");

            return true;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"[{server}].[{database}].[{schema}].[{tableName}] - Failed to write entity file for table {tableName}: {ex.Message}");
            return false;
        }
    }

    public void EnsureOutputDirectoryExists(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            ConsoleLogger.LogInfo($"Created output directory: {outputDirectory}");
        }
    }

    public void CleanOutputDirectory(string outputDirectory, bool force = false)
    {
        if (Directory.Exists(outputDirectory) && force)
        {
            Directory.Delete(outputDirectory, true);
            ConsoleLogger.LogInfo($"Cleaned output directory: {outputDirectory}");
        }
    }
}
