namespace Catalogue.Web.Services;

public enum ToastLevel
{
    Success,
    Error,
    Warning,
    Info
}

public class ToastMessage
{
    public Guid Id { get; } = Guid.NewGuid();
    public ToastLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}

public class NotificationService
{
    public event Action<ToastMessage>? OnShow;

    public void Success(string message) => Notify(ToastLevel.Success, message);
    public void Error(string message)   => Notify(ToastLevel.Error,   message);
    public void Warning(string message) => Notify(ToastLevel.Warning, message);
    public void Info(string message)    => Notify(ToastLevel.Info,    message);

    private void Notify(ToastLevel level, string message)
        => OnShow?.Invoke(new ToastMessage { Level = level, Message = message });
}
