namespace DataManager.Core.Utilities;

/// <summary>
/// Shared helper methods used by both <c>EntityClassGenerator</c> and
/// <c>EntityConfigurationGenerator</c> to keep default-value parsing and
/// backing-field naming in a single, testable place.
/// </summary>
public static class EntityGenerationHelpers
{
    /// <summary>
    /// Converts a PascalCase property name into a private backing-field name,
    /// e.g. <c>IsActive</c> → <c>_isActive</c>.
    /// Handles the edge case of C#-keyword-escaped names that start with <c>@</c>.
    /// </summary>
    public static string GenerateBackingFieldName(string propertyName)
    {
        // Remove @ prefix if present (C# keyword escaping)
        var cleanPropertyName = propertyName.TrimStart('@');
        return $"_{char.ToLower(cleanPropertyName[0])}{cleanPropertyName.Substring(1)}";
    }

    /// <summary>
    /// Returns <c>true</c> when the SQL default value expression represents a
    /// boolean <c>true</c> (i.e. <c>1</c> or the word <c>true</c>).
    /// Handles common SQL Server wrapping styles: <c>((1))</c>, <c>(1)</c>, <c>'1'</c>.
    /// </summary>
    public static bool DetermineDefaultBoolValue(string defaultValue)
    {
        if (string.IsNullOrEmpty(defaultValue))
            return false;

        var cleanValue = StripSqlDefaultWrapping(defaultValue);
        return cleanValue == "1" || cleanValue.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a SQL default value expression as a <c>long</c>, or returns
    /// <c>null</c> if the expression is not a plain integer literal.
    /// Handles wrapping styles: <c>((0))</c>, <c>(-1)</c>, <c>'-1'</c>.
    /// </summary>
    public static long? DetermineDefaultIntValue(string defaultValue)
    {
        if (string.IsNullOrEmpty(defaultValue))
            return null;

        var cleanValue = StripSqlDefaultWrapping(defaultValue);
        return long.TryParse(cleanValue, out var result) ? result : null;
    }

    /// <summary>
    /// Translates a SQL Server default-value expression to its SQLite equivalent.
    /// Returns <c>null</c> when the expression has no meaningful SQLite translation
    /// (the caller should omit the <c>HasDefaultValueSql</c> call in that case).
    /// </summary>
    public static string? TranslateSqlDefaultForSQLite(string sqlDefaultValue)
    {
        if (string.IsNullOrEmpty(sqlDefaultValue))
            return null;

        var clean = sqlDefaultValue.Trim();

        // ── Date / time server functions ──────────────────────────────────────
        var dateFunctions = new[]
        {
            "(getdate())", "(getutcdate())", "(sysdatetime())", "(sysutcdatetime())",
            "getdate()", "getutcdate()", "sysdatetime()", "sysutcdatetime()"
        };
        foreach (var fn in dateFunctions)
        {
            if (clean.Equals(fn, StringComparison.OrdinalIgnoreCase))
                return "(datetime('now','localtime'))";
        }

        var utcFunctions = new[] { "(getutcdate())", "(sysutcdatetime())", "getutcdate()", "sysutcdatetime()" };
        foreach (var fn in utcFunctions)
        {
            if (clean.Equals(fn, StringComparison.OrdinalIgnoreCase))
                return "(datetime('now'))";
        }

        // ── GUID / newid() ────────────────────────────────────────────────────
        if (clean.Equals("(newid())", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("newid()", StringComparison.OrdinalIgnoreCase))
        {
            return "(lower(hex(randomblob(16))))";
        }

        // ── Numeric literals: ((0)), ((1)), ((-1)), (0), (1), etc. ───────────
        var stripped = StripSqlDefaultWrapping(clean);
        if (long.TryParse(stripped, out _))
        {
            // Plain integer literal — valid in SQLite
            return $"({stripped})";
        }

        // ── Empty / null string literals: (N''), (''), (NULL) ─────────────────
        if (stripped.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            return "NULL";

        // Strip N prefix for nvarchar string literals
        if (stripped.StartsWith("N'") && stripped.EndsWith("'"))
            stripped = stripped.Substring(1); // remove N prefix, keep the quoted string

        if (stripped.StartsWith("'") && stripped.EndsWith("'"))
            return stripped; // valid SQLite string literal

        // ── Anything else is SQL Server-specific — omit ───────────────────────
        return null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Removes outer parentheses (up to two layers) and quote characters from
    /// a SQL default value expression, returning the bare value.
    /// E.g. <c>((0))</c> → <c>0</c>, <c>('-1')</c> → <c>-1</c>.
    /// </summary>
    private static string StripSqlDefaultWrapping(string value)
    {
        return value.Trim()
            .TrimStart('(').TrimEnd(')')
            .TrimStart('(').TrimEnd(')')
            .Trim('\'', '"', ' ');
    }
}
