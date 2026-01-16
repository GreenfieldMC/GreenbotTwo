namespace GreenbotTwo.Configuration.Models.Endpoints;

public class ApiEndpointSettings
{
    public required BasicEndpoint MojangApi { get; init; }
    public required BasicEndpoint AuthenticationHubApi { get; init; }
    public required ClientCredentialsEndpoint GreenfieldCoreApi { get; init; }

}