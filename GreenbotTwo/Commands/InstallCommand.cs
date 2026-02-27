using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class InstallCommand(IOptions<InstallCommandSettings> settings, ILogger<IApplicationCommandContext> commandLogger) : ApplicationCommandModule<ApplicationCommandContext>
{

    [SlashCommand("install", "Instructions to install Greenfield")]
    public async Task Install(
        [SlashCommandParameter(Name = "operating_system", Description = "Which OS are you trying to install Greenfield on?")] OperatingSystem osType, 
        [SlashCommandParameter(Name = "run_for", Description = "The discord user who needs the install instructions.")] User? runFor = null)
    {
        commandLogger.LogCommandExecution(Context, $"operating_system: {osType}, run_for: {runFor?.Id.ToString() ?? "null"}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        var userWhoNeedsInstructions = runFor ?? Context.User;
        
        var link = osType == OperatingSystem.Windows 
            ? settings.Value.WindowsInstallLink
            : settings.Value.MacInstallLink;
        
        var message = new MessageProperties()
            .WithContent($"## {userWhoNeedsInstructions.Mention()}, [here's the installation instructions for {osType}]({link})")
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsInstructions.Id));
        
        if (userWhoNeedsInstructions.Id == Context.User.Id)
        {
            commandLogger.LogCommandDebug(Context, "User requested their own installation instructions.");
            await Context.Interaction.ModifyResponseAsync(options => options.WithContent(message.Content).WithAllowedMentions(message.AllowedMentions));
            return;
        }

        await Context.Channel.SendMessageAsync(message);
    }
    
    public enum OperatingSystem
    {
        Windows,
        Mac
    }
    
}