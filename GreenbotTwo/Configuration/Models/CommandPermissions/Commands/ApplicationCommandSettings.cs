namespace GreenbotTwo.Configuration.Models.CommandPermissions.Commands;

public class ApplicationCommandSettings
{
    public required IEnumerable<ulong> RolesThatCanViewOtherUserApps { get; set; }
    
    public required IEnumerable<ulong> RolesThatCanListOtherUserApps { get; set; }

    public required IEnumerable<ulong> RolesThatCanForwardOtherUserApps { get; set; }
}