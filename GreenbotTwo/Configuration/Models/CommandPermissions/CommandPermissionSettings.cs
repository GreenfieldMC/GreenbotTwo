using GreenbotTwo.Configuration.Models.CommandPermissions.Commands;

namespace GreenbotTwo.Configuration.Models.CommandPermissions;

public class CommandPermissionSettings
{
    public required AppStatusSettings AppStatus { get; set; }
    public required ViewAppSettings ViewApp { get; set; }
}