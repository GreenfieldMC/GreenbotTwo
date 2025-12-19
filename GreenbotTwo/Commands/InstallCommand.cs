using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class InstallCommand(IOptions<InstallCommandSettings> settings) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    private readonly InstallCommandSettings _settings = settings.Value;

    [SlashCommand("install", "Instructions to install Greenfield")]
    public async Task Install(
        [SlashCommandParameter(Name = "operating_system", Description = "Which OS are you trying to install Greenfield on?")] OperatingSystem osType, 
        [SlashCommandParameter(Name = "run_for", Description = "The discord user who needs the install instructions.")] User? runFor = null)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        
        if ((runFor ?? Context.User) is not GuildUser userWhoNeedsInstructions)
        {
            await Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([GenericEmbeds.UserError("Invalid User", "The specified user is not a member of this server.")])
                .WithFlags(MessageFlags.Ephemeral));
            return;
        }
        
        var link = osType == OperatingSystem.Windows 
            ? _settings.WindowsInstallLink
            : _settings.MacInstallLink;

        _ = Context.Interaction.ModifyResponseAsync(options => options
            .WithContent($"## {userWhoNeedsInstructions.Mention()}, [here's the installation instructions for {osType}]({link})")
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsInstructions.Id))
        );
    }
    
    public enum OperatingSystem
    {
        Windows,
        Mac
    }
    
}