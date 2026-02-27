namespace GreenbotTwo.Configuration.Models.CommandPermissions.Commands;

public class AccountCommandSettings
{
    public required IEnumerable<ulong> RolesThatCanViewOtherUserAccounts { get; set; }
}