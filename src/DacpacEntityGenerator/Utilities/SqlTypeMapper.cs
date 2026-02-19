namespace DacpacEntityGenerator.Utilities;

public static class SqlTypeMapper
{
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "bit", "bool" },
        { "tinyint", "byte" },
        { "smallint", "short" },
        { "int", "int" },
        { "bigint", "long" },
        { "decimal", "decimal" },
        { "numeric", "decimal" },
        { "money", "decimal" },
        { "smallmoney", "decimal" },
        { "float", "float" },
        { "real", "float" },
        { "char", "string" },
        { "nchar", "string" },
        { "varchar", "string" },
        { "nvarchar", "string" },
        { "text", "string" },
        { "ntext", "string" },
        { "date", "DateOnly" },
        { "time", "TimeOnly" },
        { "datetime", "DateTime" },
        { "datetime2", "DateTime" },
        { "datetimeoffset", "DateTimeOffset" },
        { "smalldatetime", "DateTime" },
        { "uniqueidentifier", "Guid" },
        { "binary", "byte[]" },
        { "varbinary", "byte[]" },
        { "image", "byte[]" },
        { "xml", "string" }
    };

    public static string MapToCSharpType(string sqlType, bool isNullable, out bool needsMaxLength)
    {
        needsMaxLength = false;

        // Clean up the SQL type (remove parentheses and parameters)
        var baseType = sqlType.Split('(')[0].Trim().ToLower();

        // Check if it's a string type that might need MaxLength
        if (baseType == "char" || baseType == "nchar" || baseType == "varchar" || baseType == "nvarchar")
        {
            // Check if there's a length specified and it's not MAX
            if (sqlType.Contains("(") && !sqlType.ToUpper().Contains("MAX"))
            {
                needsMaxLength = true;
            }
        }

        if (TypeMap.TryGetValue(baseType, out var csharpType))
        {
            // For value types, add ? if nullable
            if (isNullable && IsValueType(csharpType))
            {
                return csharpType + "?";
            }
            return csharpType;
        }

        // Default to string for unknown types
        ConsoleLogger.LogWarning($"Unknown SQL type '{sqlType}', defaulting to string");
        return "string";
    }

    public static int? ExtractMaxLength(string sqlType)
    {
        // Extract length from types like varchar(50), nvarchar(255), etc.
        var match = System.Text.RegularExpressions.Regex.Match(sqlType, @"\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var length))
        {
            return length;
        }
        return null;
    }

    private static bool IsValueType(string csharpType)
    {
        // Check if the type is a value type (needs nullable ?)
        var valueTypes = new HashSet<string>
        {
            "bool", "byte", "short", "int", "long", "decimal", "double", "float",
            "DateTime", "DateOnly", "TimeOnly", "DateTimeOffset", "Guid"
        };
        return valueTypes.Contains(csharpType);
    }
}
