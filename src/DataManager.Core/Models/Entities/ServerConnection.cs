namespace DataManager.Core.Models.Entities;

public enum AuthenticationType
{
    WindowsAuth = 0,
    SqlAuth = 1,
    AzureAD = 2
}

public class ServerConnection
{
    public int ServerConnectionId { get; set; }
    public int ServerId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string? NamedInstance { get; set; }
    public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.WindowsAuth;
    public string? Username { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public Server Server { get; set; } = null!;
}
