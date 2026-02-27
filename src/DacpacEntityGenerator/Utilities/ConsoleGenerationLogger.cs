using Catalogue.Core.Abstractions;

namespace DacpacEntityGenerator.Utilities;

/// <summary>
/// Console-based implementation of <see cref="IGenerationLogger"/> that writes
/// coloured output to stdout — identical to the original static ConsoleLogger.
/// </summary>
public class ConsoleGenerationLogger : IGenerationLogger
{
    public void LogProgress(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
    }

    public void LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
    }

    public void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    public void LogInfo(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }
}
