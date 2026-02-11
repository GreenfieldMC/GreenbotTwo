using GreenbotTwo.Services;

namespace GreenbotTwo.Models.Forms;

public class AccountLinkForm(ulong discordSnowflake, AccountLinkService.UserSelectionFor source)
{
    public ulong DiscordSnowflake { get; set; } = discordSnowflake;
    public AccountLinkService.UserSelectionFor Source { get; set; } = source;

    public string? MinecraftUsername { get; set; }
    public Guid? MinecraftUuid { get; set; }
    
    public long? UserId { get; set; }
    
    public bool IsLinked => UserId.HasValue;

}