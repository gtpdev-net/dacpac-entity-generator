namespace DataManager.Core.Abstractions;

/// <summary>
/// Abstraction over generation-time logging, allowing the same pipeline to
/// emit messages to the console (CLI) or a Blazor UI event stream.
/// </summary>
public interface IGenerationLogger
{
    /// <summary>Logs a successful completion / progress message ([SUCCESS]).</summary>
    void LogProgress(string message);

    /// <summary>Logs a warning message ([WARNING]).</summary>
    void LogWarning(string message);

    /// <summary>Logs an error message ([ERROR]).</summary>
    void LogError(string message);

    /// <summary>Logs an informational message ([INFO]).</summary>
    void LogInfo(string message);
}
