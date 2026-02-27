using System.Text;
using System.Text.RegularExpressions;

namespace Dacpac.Management.Utilities;

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

    public static string Pluralize(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return word;

        var unchangables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ExternalComponentAssembly"
        };

        if (unchangables.Contains(word))
            return word;

        var irregulars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Person", "People" },
            { "Man", "Men" },
            { "Woman", "Women" },
            { "Child", "Children" },
            { "Tooth", "Teeth" },
            { "Foot", "Feet" },
            { "Mouse", "Mice" },
            { "Goose", "Geese" },
            { "Ox", "Oxen" },
            { "Datum", "Data" },
            { "Medium", "Media" },
            { "Analysis", "Analyses" },
            { "Diagnosis", "Diagnoses" },
            { "Oasis", "Oases" },
            { "Thesis", "Theses" },
            { "Crisis", "Crises" },
            { "Phenomenon", "Phenomena" },
            { "Criterion", "Criteria" },
            { "Index", "Indices" }
        };

        if (irregulars.TryGetValue(word, out var irregular))
            return irregular;

        var uncountables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Equipment", "Information", "Money", "Species", "Series", "Fish", "Sheep", "Deer",
            "Moose", "Swine", "Buffalo", "Shrimp", "Trout", "Offspring", "Aircraft", "Data"
        };

        foreach (var uncountable in uncountables)
        {
            if (word.EndsWith(uncountable, StringComparison.OrdinalIgnoreCase))
                return word;
        }

        foreach (var pluralForm in irregulars.Values)
        {
            if (word.EndsWith(pluralForm, StringComparison.OrdinalIgnoreCase) && word.Length > pluralForm.Length)
                return word;
        }

        if (word.Length > 3)
        {
            if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase)) return word;
            if (word.EndsWith("ves", StringComparison.OrdinalIgnoreCase)) return word;
            if (word.EndsWith("ses", StringComparison.OrdinalIgnoreCase)) return word;
            if (word.EndsWith("xes", StringComparison.OrdinalIgnoreCase)) return word;
            if (word.EndsWith("zes", StringComparison.OrdinalIgnoreCase)) return word;
            if (word.EndsWith("ches", StringComparison.OrdinalIgnoreCase)) return word;
            if (word.EndsWith("shes", StringComparison.OrdinalIgnoreCase)) return word;

            if (word.EndsWith("oes", StringComparison.OrdinalIgnoreCase) &&
                word.Length >= 4 && !"aeiou".Contains(word[^4].ToString(), StringComparison.OrdinalIgnoreCase))
                return word;

            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
                !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
                word.Length >= 2 &&
                !"aeiou".Contains(word[^2].ToString(), StringComparison.OrdinalIgnoreCase))
                return word;
        }

        if (word.Length >= 2 && word.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            !"aeiou".Contains(word[^2].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return word.Substring(0, word.Length - 1) + "ies";
        }

        if (word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("z", StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }

        if (word.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            return word.Substring(0, word.Length - 1) + "ves";

        if (word.EndsWith("fe", StringComparison.OrdinalIgnoreCase))
            return word.Substring(0, word.Length - 2) + "ves";

        if (word.Length >= 2 && word.EndsWith("o", StringComparison.OrdinalIgnoreCase) &&
            !"aeiou".Contains(word[^2].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }

        return word + "s";
    }
}
