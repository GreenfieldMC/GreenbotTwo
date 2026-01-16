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

public class AuthHubAppConnections
{
    /// <summary>
    /// The list of application connections
    /// </summary>
    public required List<AuthHubAppConnection> Apps { get; set; }
}

public class AuthHubResponse
{
    public required string Message { get; set; }
    public required int Status { get; set; }
}