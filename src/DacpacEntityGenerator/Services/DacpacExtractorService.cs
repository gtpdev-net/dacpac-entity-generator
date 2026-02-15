using System.IO.Compression;
using DacpacEntityGenerator.Utilities;

namespace DacpacEntityGenerator.Services;

public class DacpacExtractorService
{
    public string? ExtractModelXml(string inputDirectory, string server, string database)
    {
        var dacpacFileName = $"{server}_{database}.dacpac";
        var dacpacPath = Path.Combine(inputDirectory, "dacpacs", dacpacFileName);

        if (!File.Exists(dacpacPath))
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - DACPAC file not found: {dacpacFileName}");
            return null;
        }

        ConsoleLogger.LogProgress($"[{server}].[{database}] - Processing DACPAC: {dacpacFileName}");

        try
        {
            using var archive = ZipFile.OpenRead(dacpacPath);
            var modelEntry = archive.Entries.FirstOrDefault(e => 
                e.FullName.Equals("model.xml", StringComparison.OrdinalIgnoreCase));

            if (modelEntry == null)
            {
                ConsoleLogger.LogError($"[{server}].[{database}] - model.xml not found in DACPAC: {dacpacFileName}");
                return null;
            }

            using var stream = modelEntry.Open();
            using var reader = new StreamReader(stream);
            var modelXml = reader.ReadToEnd();

            var sizeKB = modelXml.Length / 1024;
            ConsoleLogger.LogInfo($"[{server}].[{database}] - Extracted model.xml ({sizeKB} KB)");

            return modelXml;
        }
        catch (InvalidDataException ex)
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Corrupted DACPAC file: {dacpacFileName} - {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            ConsoleLogger.LogError($"[{server}].[{database}] - Failed to extract model.xml from {dacpacFileName}: {ex.Message}");
            return null;
        }
    }

    public bool DacpacExists(string inputDirectory, string server, string database)
    {
        var dacpacFileName = $"{server}_{database}.dacpac";
        var dacpacPath = Path.Combine(inputDirectory, "dacpacs", dacpacFileName);
        return File.Exists(dacpacPath);
    }
}
