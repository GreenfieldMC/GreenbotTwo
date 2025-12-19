namespace GreenbotTwo.Configuration.Models.Endpoints;

public class ClientCredentialsEndpoint : BasicEndpoint
{
    public required string TokenEndpoint { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required int RefreshSkewSeconds { get; init; } = 60;
}