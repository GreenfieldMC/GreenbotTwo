using GreenbotTwo.Configuration.Models.CommandPermissions.Commands;

namespace GreenbotTwo.Configuration.Models.CommandPermissions;

public class CommandPermissionSettings
{
    public required AccountCommandSettings AccountCommand { get; set; }
    public required ApplicationCommandSettings ApplicationCommand { get; set; }
}