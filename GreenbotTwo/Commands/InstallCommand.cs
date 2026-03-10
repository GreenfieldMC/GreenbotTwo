using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class InstallCommand(IOptions<InstallCommandSettings> settings, ILogger<IApplicationCommandContext> commandLogger, RestClient restClient) : ApplicationCommandModule<ApplicationCommandContext>
{
    private static readonly Func<ulong, EmbedProperties> SentToOtherUser = recipient => GenericEmbeds.Success("Installation Instructions", $"The installation instructions have been sent to {recipient.Mention()}.");
    private static readonly Func<ulong, string, EmbedProperties> ErrorUnableToSendToOtherUser = (recipient, error) => GenericEmbeds.UserError("Installation Instructions", $"Unable to send the installation instructions to {recipient.Mention()}. Error: {error}");

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
            commandLogger.LogInteractionDebug(Context, "User requested their own installation instructions.");
            await Context.Interaction.ModifyResponseAsync(options => options.WithContent(message.Content).WithAllowedMentions(message.AllowedMentions));
            return;
        }

        var dmChannel = await restClient.GetDMChannelAsync(userWhoNeedsInstructions.Id);
        try
        {
            await dmChannel.SendMessageAsync(message);
        }
        catch (Exception e)
        {
            await Context.Interaction.ModifyResponse([ErrorUnableToSendToOtherUser(userWhoNeedsInstructions.Id, e.Message)]);
            return;
        }
        
        await Context.Interaction.ModifyResponse([SentToOtherUser(userWhoNeedsInstructions.Id)], flags: MessageFlags.Ephemeral);
    }
    
    public enum OperatingSystem
    {
        Windows,
        Mac
    }
    
}