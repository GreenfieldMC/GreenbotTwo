namespace GreenbotTwo.Models.GreenfieldApi;

public class ConnectionModels
{

    public record ApiDiscordConnection
    {
        public required long DiscordConnectionId { get; init; }
        public required ulong DiscordSnowflake { get; init; }
        public required string DiscordUsername { get; init; }
        public required DateTime? UpdatedOn { get; init; }
        public required DateTime CreatedOn { get; init; }
    }

    public record ApiDiscordConnectionWithUsers : ApiDiscordConnection
    {
        public required List<User> Users { get; init; }
    }
    
    public record ApiDiscordAccount : ApiDiscordConnection
    {
        public required long UserDiscordConnectionId { get; init; }
        public required User User { get; init; }
        public required DateTime ConnectedOn { get; init; }
    }
    
    public record ApiPatreonConnection
    {
        public required long PatreonConnectionId { get; init; }
        public required string FullName { get; init; }
        public required decimal? Pledge { get; init; }
        public required DateTime? UpdatedOn { get; init; }
        public required DateTime CreatedOn { get; init; }
    }
    
    public record ApiPatreonConnectionWithUsers : ApiPatreonConnection
    {
        public required IEnumerable<User> Users { get; init; }
    }
    
    public record ApiPatreonAccount : ApiPatreonConnection
    {
        public required long UserPatreonConnectionId { get; init; }
        public required User User { get; init; }
        public required DateTime ConnectedOn { get; init; }
    }
    
}