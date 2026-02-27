using System.IO.Compression;
using Catalogue.Core.Abstractions;

namespace Dacpac.Management.Services;

public class DacpacExtractorService
{
    private readonly IGenerationLogger _logger;

    public DacpacExtractorService(IGenerationLogger logger)
    {
        _logger = logger;
    }

    public string? ExtractModelXml(string inputDirectory, string server, string database)
    {
        var dacpacFileName = $"{server}_{database}.dacpac";
        var dacpacPath = Path.Combine(inputDirectory, "dacpacs", dacpacFileName);

        if (!File.Exists(dacpacPath))
        {
            _logger.LogError($"[{server}].[{database}] - DACPAC file not found: {dacpacFileName}");
            return null;
        }

        _logger.LogProgress($"[{server}].[{database}] - Processing DACPAC: {dacpacFileName}");

        return ExtractModelXmlFromPath(dacpacPath, server, database);
    }

    public string? ExtractModelXmlFromPath(string dacpacPath, string server, string database)
    {
        try
        {
            using var archive = ZipFile.OpenRead(dacpacPath);
            var modelEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals("model.xml", StringComparison.OrdinalIgnoreCase));

            if (modelEntry == null)
            {
                _logger.LogError($"[{server}].[{database}] - model.xml not found in DACPAC");
                return null;
            }

            using var stream = modelEntry.Open();
            using var reader = new StreamReader(stream);
            var modelXml = reader.ReadToEnd();

            var sizeKB = modelXml.Length / 1024;
            _logger.LogInfo($"[{server}].[{database}] - Extracted model.xml ({sizeKB} KB)");

            return modelXml;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError($"[{server}].[{database}] - Corrupted DACPAC: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to extract model.xml: {ex.Message}");
            return null;
        }
    }

    public string? ExtractModelXmlFromStream(Stream dacpacStream, string server, string database)
    {
        try
        {
            using var archive = new ZipArchive(dacpacStream, ZipArchiveMode.Read, leaveOpen: true);
            var modelEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals("model.xml", StringComparison.OrdinalIgnoreCase));

            if (modelEntry == null)
            {
                _logger.LogError($"[{server}].[{database}] - model.xml not found in DACPAC stream");
                return null;
            }

            using var stream = modelEntry.Open();
            using var reader = new StreamReader(stream);
            var modelXml = reader.ReadToEnd();

            var sizeKB = modelXml.Length / 1024;
            _logger.LogInfo($"[{server}].[{database}] - Extracted model.xml from stream ({sizeKB} KB)");

            return modelXml;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{server}].[{database}] - Failed to extract model.xml from stream: {ex.Message}");
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
