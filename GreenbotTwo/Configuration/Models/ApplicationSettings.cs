using GreenbotTwo.Configuration.Models.CommandPermissions;
using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Configuration.Models.Endpoints;

namespace GreenbotTwo.Configuration.Models;

public class ApplicationSettings
{
    public required DiscordSettings Discord { get; set; }
    public required CommandSettings CommandSettings { get; set; }
    public required ApiEndpointSettings ApiEndpoints { get; set; }
    public required BuilderApplicationSettings BuilderApplicationSettings { get; set; }
    public required CommandPermissionSettings CommandPermissions { get; set; }
}