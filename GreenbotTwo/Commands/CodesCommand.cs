using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class CodesCommand(IGreenfieldApiService greenfieldApiService, ILogger<IApplicationCommandContext> commandLogger, RestClient restClient) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    private static readonly EmbedProperties ErrorCouldNotFetchBuildCodes = GenericEmbeds.InternalError("Internal Application Error", "An error occurred while fetching build codes. Please try again later.");
    private static readonly EmbedProperties ErrorNoBuildCodesDefined = GenericEmbeds.UserError("Current Server Build Codes", "There are currently no build codes available.");
    private static readonly Func<ulong, EmbedProperties> SentToOtherUser = recipient => GenericEmbeds.Success("Current Server Build Codes", $"The current server build codes have been sent to {recipient.Mention()}.");
    private static readonly Func<ulong, string, EmbedProperties> ErrorUnableToSendToOtherUser = (recipient, error) => GenericEmbeds.UserError("Current Server Build Codes", $"Unable to send the current server build codes to {recipient.Mention()}. Error: {error}");
    
    [SlashCommand("codes", "Get all build codes from Greenfield")]
    public async Task Codes(
        [SlashCommandParameter(Name = "run_for", Description = "The discord user who needs the install instructions.")] User? runFor = null)
    {
        var userWhoNeedsCodes = runFor ?? Context.User;
        commandLogger.LogCommandExecution(Context, $"run_for: {userWhoNeedsCodes.Username}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var buildCodeResult = await greenfieldApiService.GetBuildCodes();   
        
        if (!buildCodeResult.TryGetDataNonNull(out var buildCodesEnum))
        {
            commandLogger.LogInteractionDebug(Context, $"Failed to fetch build codes with error: {buildCodeResult.ErrorMessage}, Status Code: {buildCodeResult.StatusCode}");
            await Context.Interaction.ModifyResponse([ErrorCouldNotFetchBuildCodes]);
            return;
        }

        var buildCodes = buildCodesEnum.ToList();

        if (buildCodes.Count == 0)
        {
            commandLogger.LogInteractionDebug(Context, "No build codes are defined for the server.");
            await Context.Interaction.ModifyResponse([ErrorNoBuildCodesDefined]);
            return;
        }

        var message = new MessageProperties()
            .WithContent(userWhoNeedsCodes.Mention())
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsCodes.Id))
            .WithEmbeds([
                new EmbedProperties()
                    .WithTitle("Current Server Build Codes")
                    .WithFields(buildCodes
                        .OrderBy(c => c.ListOrder)
                        .Select((code, idx) => new EmbedFieldProperties().WithInline(false).WithName(" ")
                            .WithValue($"**__{idx + 1}.__)** {code.Code}"))
                    )
            ])
            .WithFlags(MessageFlags.Ephemeral);
        
        if (userWhoNeedsCodes.Id == Context.User.Id)
        {
            commandLogger.LogInteractionDebug(Context, "User requested their own build codes.");
            await Context.Interaction.ModifyResponse([
                new EmbedProperties()
                    .WithTitle("Current Server Build Codes")
                    .WithFields(buildCodes
                        .OrderBy(c => c.ListOrder)
                        .Select((code, idx) => new EmbedFieldProperties().WithInline(false).WithName(" ")
                            .WithValue($"**__{idx + 1}.__)** {code.Code}"))
                    )
            ]);
            return;
        }

        var dmChannel = await restClient.GetDMChannelAsync(userWhoNeedsCodes.Id);
        try
        {
            await dmChannel.SendMessageAsync(message);
        }
        catch (Exception e)
        {
            await Context.Interaction.ModifyResponse([ErrorUnableToSendToOtherUser(userWhoNeedsCodes.Id, e.Message)]);
            return;
        }
        
        await Context.Interaction.ModifyResponse([SentToOtherUser(userWhoNeedsCodes.Id)], flags: MessageFlags.Ephemeral);
    }
}