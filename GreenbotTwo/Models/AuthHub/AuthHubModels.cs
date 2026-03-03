using System.Text.Json.Serialization;

namespace GreenbotTwo.Models.AuthHub;

public class AuthHubAppConnection
{
    /// <summary>
    /// The name of the application connection
    /// </summary>
    public required string Name { get; set; }
    /// <summary>
    /// Either the connect or disconnect url
    /// </summary>
    public required string Connection { get; set; }
    /// <summary>
    /// Whether this application is currently "connected" or "disconnected"
    /// </summary>
    public required string Status { get; set; }
}

public class AuthHubInfo
{
    [JsonPropertyName("auth_server_ip")]
    public required string AuthServerIp { get; set; }
    
    [JsonPropertyName("auth_server_version")]
    public required string AuthServerVersion { get; set; }
}