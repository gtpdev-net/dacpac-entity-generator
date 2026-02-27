using DataManager.Core.Abstractions;

namespace DataManager.Web.Services;

/// <summary>
/// Blazor-compatible logger that fires an event for each log entry so
/// the EF Generation page can stream messages into the UI in real time.
/// Registered as Scoped — one instance per Blazor circuit.
/// </summary>
public class BlazorGenerationLogger : IGenerationLogger
{
    /// <summary>
    /// Raised on every log call.  Parameters: (level, message)
    /// where level is "progress" | "warning" | "error" | "info".
    /// </summary>
    public event Action<string, string>? OnLog;

    public void LogProgress(string message) => OnLog?.Invoke("progress", message);
    public void LogWarning(string message)  => OnLog?.Invoke("warning",  message);
    public void LogError(string message)    => OnLog?.Invoke("error",    message);
    public void LogInfo(string message)     => OnLog?.Invoke("info",     message);
}
