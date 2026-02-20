using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class CodesCommand(IGreenfieldApiService greenfieldApiService, ILogger<IApplicationCommandContext> commandLogger) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    private static readonly EmbedProperties ErrorCouldNotFetchBuildCodes = GenericEmbeds.InternalError("Internal Application Error", "An error occurred while fetching build codes. Please try again later.");
    private static readonly EmbedProperties ErrorNoBuildCodesDefined = GenericEmbeds.UserError("Current Server Build Codes", "There are currently no build codes available.");
    
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
            commandLogger.LogCommandDebug(Context, $"Failed to fetch build codes with error: {buildCodeResult.ErrorMessage}, Status Code: {buildCodeResult.StatusCode}");
            await Context.Interaction.ModifyResponse([ErrorCouldNotFetchBuildCodes]);
            return;
        }

        var buildCodes = buildCodesEnum.ToList();

        if (buildCodes.Count == 0)
        {
            commandLogger.LogCommandDebug(Context, "No build codes are defined for the server.");
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
            commandLogger.LogCommandDebug(Context, "User requested their own build codes.");
            await Context.Interaction.ModifyResponseAsync(_ => message.ToInteractionMessageProperties());
            return;
        }

        await Context.Interaction.DeleteResponseAsync();
        await Context.Channel.SendMessageAsync(message);
    }
}