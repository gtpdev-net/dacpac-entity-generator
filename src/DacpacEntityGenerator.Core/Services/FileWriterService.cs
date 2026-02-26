using System.Text;
using DacpacEntityGenerator.Core.Abstractions;
using DacpacEntityGenerator.Core.Models;
using DacpacEntityGenerator.Core.Utilities;

namespace DacpacEntityGenerator.Core.Services;

public class FileWriterService
{
    private readonly IGenerationLogger _logger;

    public FileWriterService(IGenerationLogger logger)
    {
        _logger = logger;
    }

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
            _logger.LogProgress($"[{server}].[{database}].[{schema}].[{tableName}] - Generated entity: ./output/{relativePath.Replace('\\', '/')}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}].[{schema}].[{tableName}] - Failed to write entity file for table {tableName}: {ex.Message}");
            return false;
        }
    }

    public void EnsureOutputDirectoryExists(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            _logger.LogInfo($"Created output directory: {outputDirectory}");
        }
    }

    public void CleanOutputDirectory(string outputDirectory, bool force = false)
    {
        if (Directory.Exists(outputDirectory) && force)
        {
            Directory.Delete(outputDirectory, true);
            _logger.LogInfo($"Cleaned output directory: {outputDirectory}");
        }
    }

    public bool WriteConfigurationFile(
        string outputDirectory,
        string server,
        string database,
        string configurationClassCode)
    {
        try
        {
            // Create output directory structure: ./output/Configuration/{Server}/{Database}/
            var configDir = Path.Combine(outputDirectory, "Configuration");
            var serverDir = Path.Combine(configDir, server);
            var databaseDir = Path.Combine(serverDir, database);
            
            Directory.CreateDirectory(databaseDir);

            // Generate filename: {Database}EntityConfiguration.cs (PascalCase)
            var databasePascal = NameConverter.ToPascalCase(database);
            var fileName = $"{databasePascal}EntityConfiguration.cs";
            var filePath = Path.Combine(databaseDir, fileName);

            // Write file with UTF-8 encoding
            File.WriteAllText(filePath, configurationClassCode, Encoding.UTF8);

            // Get relative path for logging
            var relativePath = Path.GetRelativePath(outputDirectory, filePath);
            _logger.LogProgress($"[{server}].[{database}] - Generated configuration: ./output/{relativePath.Replace('\\', '/')}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to write configuration file: {ex.Message}");
            return false;
        }
    }

    public bool WriteViewFile(
        string outputDirectory,
        string server,
        string database,
        string schema,
        string viewName,
        string viewClassCode)
    {
        try
        {
            // Create output directory structure: ./output/{Server}/{Database}/Views/
            var serverDir = Path.Combine(outputDirectory, server);
            var databaseDir = Path.Combine(serverDir, database);
            var viewsDir = Path.Combine(databaseDir, "Views");
            
            Directory.CreateDirectory(viewsDir);

            // Generate filename: {ViewName}.cs (PascalCase)
            var className = NameConverter.ToPascalCase(viewName);
            var fileName = $"{className}.cs";
            var filePath = Path.Combine(viewsDir, fileName);

            // Write file with UTF-8 encoding
            File.WriteAllText(filePath, viewClassCode, Encoding.UTF8);

            // Get relative path for logging
            var relativePath = Path.GetRelativePath(outputDirectory, filePath);
            _logger.LogProgress($"[{server}].[{database}].[{schema}].[{viewName}] - Generated view entity: ./output/{relativePath.Replace('\\', '/')}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}].[{schema}].[{viewName}] - Failed to write view file: {ex.Message}");
            return false;
        }
    }

    public bool WriteDbContextFile(
        string outputDirectory,
        string dbContextCode)
    {
        try
        {
            // Create output directory structure: ./output/
            Directory.CreateDirectory(outputDirectory);

            // Generate filename: SQLDbContext.cs
            var fileName = "SQLDbContext.cs";
            var filePath = Path.Combine(outputDirectory, fileName);

            // Write file with UTF-8 encoding
            File.WriteAllText(filePath, dbContextCode, Encoding.UTF8);

            // Get relative path for logging
            var relativePath = Path.GetRelativePath(outputDirectory, filePath);
            _logger.LogProgress($"Generated DbContext: ./output/{relativePath.Replace('\\', '/')}");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write DbContext file: {ex.Message}");
            return false;
        }
    }
}
