using System.Text;
using System.Text.RegularExpressions;

namespace DataManager.Core.Utilities;

public static class NameConverter
{
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    public static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var parts = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var result = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;

            result.Append(char.ToUpper(part[0]));
            if (part.Length > 1)
                result.Append(part.Substring(1));
        }

        return SanitizeIdentifier(result.ToString());
    }

    public static string SanitizeIdentifier(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "_";

        var sanitized = Regex.Replace(input, @"[^\w]", "");

        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        if (string.IsNullOrEmpty(sanitized))
            sanitized = "_";

        if (CSharpKeywords.Contains(sanitized))
            sanitized = "@" + sanitized;

        return sanitized;
    }
}
