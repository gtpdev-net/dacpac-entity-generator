using System.Text;
using System.Text.RegularExpressions;

namespace DacpacEntityGenerator.Utilities;

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

        // Split on underscores, hyphens, and spaces
        var parts = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        
        var result = new StringBuilder();
        foreach (var part in parts)
        {
            if (string.IsNullOrEmpty(part))
                continue;
                
            // Capitalize first letter, keep rest as is (to preserve existing casing)
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

        // Remove invalid characters (keep only letters, digits, underscores)
        var sanitized = Regex.Replace(input, @"[^\w]", "");
        
        // If starts with digit, prefix with underscore
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // If empty after sanitization, use default
        if (string.IsNullOrEmpty(sanitized))
            sanitized = "_";

        // Handle C# keywords by prefixing with @
        if (CSharpKeywords.Contains(sanitized))
            sanitized = "@" + sanitized;

        return sanitized;
    }

    public static string Pluralize(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return word;

        // Common irregular plurals
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

        // Check for irregular plurals
        if (irregulars.TryGetValue(word, out var irregular))
            return irregular;

        // Uncountable nouns (same in plural form)
        var uncountables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Equipment", "Information", "Money", "Species", "Series", "Fish", "Sheep", "Deer",
            "Moose", "Swine", "Buffalo", "Shrimp", "Trout", "Offspring", "Aircraft", "Data"
        };

        if (uncountables.Contains(word))
            return word;

        // Check if word appears to already be plural
        // Words ending in common plural patterns
        if (word.Length > 3)
        {
            // Words ending in "ies" (e.g., Categories, Entries)
            if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "ves" (e.g., Knives, Wolves)
            if (word.EndsWith("ves", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "ses" (e.g., Analyses, Cases, Processes)
            if (word.EndsWith("ses", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "xes" (e.g., Boxes, Indexes)
            if (word.EndsWith("xes", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "zes" (e.g., Quizzes, Prizes)
            if (word.EndsWith("zes", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "ches" (e.g., Matches, Branches)
            if (word.EndsWith("ches", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "shes" (e.g., Dishes, Wishes)
            if (word.EndsWith("shes", StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words ending in "oes" (e.g., Heroes, Potatoes) - but not "oes" where o comes after vowel
            if (word.EndsWith("oes", StringComparison.OrdinalIgnoreCase) && 
                word.Length >= 4 && !"aeiou".Contains(word[^4].ToString(), StringComparison.OrdinalIgnoreCase))
                return word;
            
            // Words clearly ending in plural 's' preceded by consonant (e.g., Errors, Items, Records)
            // But not words ending in 'ss' (e.g., Class -> Classes)
            if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) && 
                !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
                word.Length >= 2 &&
                !"aeiou".Contains(word[^2].ToString(), StringComparison.OrdinalIgnoreCase))
                return word;
        }

        // Apply standard English pluralization rules
        // Words ending in 'y' preceded by consonant: change y to ies
        if (word.Length >= 2 && word.EndsWith("y", StringComparison.OrdinalIgnoreCase) && 
            !"aeiou".Contains(word[^2].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return word.Substring(0, word.Length - 1) + "ies";
        }

        // Words ending in 's', 'ss', 'sh', 'ch', 'x', 'z': add es
        if (word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("z", StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }

        // Words ending in 'f' or 'fe': change to ves
        if (word.EndsWith("f", StringComparison.OrdinalIgnoreCase))
        {
            return word.Substring(0, word.Length - 1) + "ves";
        }
        if (word.EndsWith("fe", StringComparison.OrdinalIgnoreCase))
        {
            return word.Substring(0, word.Length - 2) + "ves";
        }

        // Words ending in 'o' preceded by consonant: typically add es
        if (word.Length >= 2 && word.EndsWith("o", StringComparison.OrdinalIgnoreCase) && 
            !"aeiou".Contains(word[^2].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }

        // Default: just add 's'
        return word + "s";
    }
}
