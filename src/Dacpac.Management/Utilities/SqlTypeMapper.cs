using Catalogue.Core.Abstractions;

namespace Dacpac.Management.Utilities;

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

    public static string MapToCSharpType(string sqlType, bool isNullable, out bool needsMaxLength, IGenerationLogger? logger = null)
    {
        needsMaxLength = false;

        var baseType = sqlType.Split('(')[0].Trim().ToLower();

        if (baseType == "char" || baseType == "nchar" || baseType == "varchar" || baseType == "nvarchar")
        {
            if (sqlType.Contains("(") && !sqlType.ToUpper().Contains("MAX"))
            {
                needsMaxLength = true;
            }
        }

        if (TypeMap.TryGetValue(baseType, out var csharpType))
        {
            if (isNullable && IsValueType(csharpType))
            {
                return csharpType + "?";
            }
            return csharpType;
        }

        logger?.LogWarning($"Unknown SQL type '{sqlType}', defaulting to string");
        return "string";
    }

    public static int? ExtractMaxLength(string sqlType)
    {
        var match = System.Text.RegularExpressions.Regex.Match(sqlType, @"\((\d+)\)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var length))
        {
            return length;
        }
        return null;
    }

    private static bool IsValueType(string csharpType)
    {
        var valueTypes = new HashSet<string>
        {
            "bool", "byte", "short", "int", "long", "decimal", "double", "float",
            "DateTime", "DateOnly", "TimeOnly", "DateTimeOffset", "Guid"
        };
        return valueTypes.Contains(csharpType);
    }
}
