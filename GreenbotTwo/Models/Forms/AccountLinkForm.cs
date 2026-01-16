namespace GreenbotTwo.Models.Forms;

public class AccountLinkForm(ulong discordSnowflake)
{
    public ulong DiscordSnowflake { get; set; } = discordSnowflake;

    public string? MinecraftUsername { get; set; }
    public Guid? MinecraftUuid { get; set; }

}