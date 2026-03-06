using System.Text;
using System.Text.RegularExpressions;
using DataManager.Core.Abstractions;

namespace DataManager.Infrastructure.Generation;

/// <summary>
/// Reads a CSV of existing DbSet definitions (old names) and the generated
/// DbContext (new names), joins them on FULLY_QUALIFIED_CLASS_NAME, then
/// replaces every <c>_context.OldName</c> usage in the target codebase with
/// <c>_context.NewName</c>.
/// </summary>
public class DbSetReplacementService
{
    private static readonly Regex DbSetLineRegex = new(
        @"public\s+DbSet<(.+?)>\s+(\w+)\s*\{",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "node_modules", "packages"
    };

    private readonly IGenerationLogger _logger;

    public DbSetReplacementService(IGenerationLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds a mapping of old DbSet property names to new ones by matching
    /// on the fully-qualified entity class name, then replaces all
    /// <c>_context.OldName</c> usages in .cs files under the target directory.
    /// </summary>
    /// <param name="csvFilePath">
    /// Path to a CSV file whose rows contain (in order):
    /// complete DbSet line, FULLY_QUALIFIED_CLASS_NAME, DB_SET_NAME.
    /// A header row is expected and skipped.
    /// </param>
    /// <param name="generatedDbContextFilePath">
    /// Path to the generated DbContext .cs file from which new DbSet names
    /// are extracted.
    /// </param>
    /// <param name="targetDirectory">
    /// Root directory of the target codebase to scan for usages.
    /// </param>
    public DbSetReplacementResult ReplaceDbSetUsages(
        string csvFilePath,
        string generatedDbContextFilePath,
        string targetDirectory)
    {
        var result = new DbSetReplacementResult();

        // 1. Parse old FQCN → DbSetName from CSV
        var oldMappings = ParseCsv(csvFilePath);
        _logger.LogInfo($"Parsed {oldMappings.Count} old DbSet mapping(s) from CSV.");

        // 2. Parse new FQCN → DbSetName from generated DbContext
        var generatedContent = File.ReadAllText(generatedDbContextFilePath);
        var newMappings = ParseDbSetDefinitions(generatedContent);
        _logger.LogInfo($"Parsed {newMappings.Count} new DbSet mapping(s) from generated DbContext.");

        // 3. Build old→new replacement map via FQCN join
        var replacements = BuildReplacementMap(oldMappings, newMappings, result);

        // 4. Apply replacements across target codebase
        ApplyReplacements(targetDirectory, replacements, result);

        return result;
    }

    // ── CSV parsing ──────────────────────────────────────────────────────────

    private Dictionary<string, string> ParseCsv(string csvFilePath)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(csvFilePath);

        foreach (var line in lines.Skip(1)) // skip header
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseCsvLine(line);
            if (fields.Count < 3)
            {
                _logger.LogWarning($"Skipping malformed CSV line: {line}");
                continue;
            }

            var fqcn      = fields[1].Trim();
            var dbSetName = fields[2].Trim();

            if (!string.IsNullOrEmpty(fqcn) && !string.IsNullOrEmpty(dbSetName))
                mappings[fqcn] = dbSetName;
        }

        return mappings;
    }

    /// <summary>
    /// Minimal CSV field parser that handles quoted fields containing commas.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields  = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    // ── Generated DbContext parsing ──────────────────────────────────────────

    /// <summary>
    /// Extracts FQCN → DbSet property name pairs from DbSet declarations
    /// in a generated DbContext file.
    /// </summary>
    internal Dictionary<string, string> ParseDbSetDefinitions(string dbContextContent)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match match in DbSetLineRegex.Matches(dbContextContent))
        {
            var fqcn      = match.Groups[1].Value.Trim();
            var dbSetName = match.Groups[2].Value.Trim();
            mappings[fqcn] = dbSetName;
        }

        return mappings;
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private Dictionary<string, string> BuildReplacementMap(
        Dictionary<string, string> oldMappings,
        Dictionary<string, string> newMappings,
        DbSetReplacementResult result)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

        _logger.LogInfo("Building DbSet name replacement map...");

        foreach (var (fqcn, oldName) in oldMappings)
        {
            if (newMappings.TryGetValue(fqcn, out var newName))
            {
                if (oldName != newName)
                {
                    replacements[oldName] = newName;
                    _logger.LogProgress($"  {oldName} → {newName}");
                }
                else
                {
                    result.UnchangedCount++;
                }
            }
            else
            {
                _logger.LogWarning($"No generated DbSet found for FQCN: {fqcn} (old name: {oldName})");
                result.UnmatchedOldNames.Add(oldName);
            }
        }

        result.ReplacementsMapped = replacements.Count;
        return replacements;
    }

    // ── File replacement ─────────────────────────────────────────────────────

    private void ApplyReplacements(
        string targetDirectory,
        Dictionary<string, string> replacements,
        DbSetReplacementResult result)
    {
        if (replacements.Count == 0)
        {
            _logger.LogInfo("No DbSet name replacements needed.");
            return;
        }

        // Pre-compile one regex per old name for word-boundary-safe matching
        var regexReplacements = replacements
            .Select(kvp => (
                Pattern:     new Regex($@"_context\.{Regex.Escape(kvp.Key)}\b", RegexOptions.Compiled),
                Replacement: $"_context.{kvp.Value}"))
            .ToList();

        _logger.LogInfo($"Applying {replacements.Count} replacement(s) across {targetDirectory} ...");

        foreach (var filePath in EnumerateCsFiles(targetDirectory))
        {
            var content  = File.ReadAllText(filePath);
            var modified = content;

            foreach (var (pattern, replacement) in regexReplacements)
            {
                modified = pattern.Replace(modified, replacement);
            }

            if (modified != content)
            {
                File.WriteAllText(filePath, modified, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                result.FilesModified++;
                _logger.LogProgress($"  Updated: {Path.GetRelativePath(targetDirectory, filePath)}");
            }
        }
    }

    private static IEnumerable<string> EnumerateCsFiles(string rootDirectory)
    {
        return Directory.EnumerateFiles(rootDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var relativePath = Path.GetRelativePath(rootDirectory, f);
                var parts = relativePath.Split(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return !parts.Any(p => ExcludedDirectories.Contains(p));
            });
    }
}

/// <summary>
/// Summary of a DbSet-usage replacement run.
/// </summary>
public class DbSetReplacementResult
{
    /// <summary>Number of old→new name pairs that differed and were applied.</summary>
    public int ReplacementsMapped { get; set; }

    /// <summary>Number of DbSets whose old and new names were already identical.</summary>
    public int UnchangedCount { get; set; }

    /// <summary>Number of .cs files that were modified.</summary>
    public int FilesModified { get; set; }

    /// <summary>Old DbSet names whose FQCN had no match in the generated DbContext.</summary>
    public List<string> UnmatchedOldNames { get; set; } = new();
}
