using GreenbotTwo.Configuration.Models.CommandPermissions.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Preconditions;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands.ApplicationCommands;

public class ViewAppCommand(IApplicationService applicationService, IGreenfieldApiService gfApiService) : ApplicationCommandModule<ApplicationCommandContext>
{

    private static readonly EmbedProperties ErrorApplicationNotFound = GenericEmbeds.UserError("Greenfield Application Service", 
        "The application with the provided ID was not found. Please double-check the ID and try again.");
    private static readonly Func<string, EmbedProperties> ErrorUserNotFound = (errorMessage) => GenericEmbeds.InternalError("Greenfield Application Service", 
        $"An internal error occurred while trying to fetch the user who submitted this application. Error: {errorMessage}. Please try again later.");
    
    [SlashCommand("viewapp", "View a submitted application by its ID")]
    public async Task View(
        [ApplicationOwnerOrRanked<ViewAppSettings>("RequiredRoleForOtherUserViewing")] 
        [SlashCommandParameter(Description = "The ID of the application to view.")]
        long applicationId)
    {
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var foundAppResponse = await gfApiService.GetApplicationById(applicationId);

        if (!foundAppResponse.TryGetDataNonNull(out var application))
        {
            await Context.Interaction.ModifyResponse([ErrorApplicationNotFound]);
            return;
        }
        
        var userDiscordAccountResponse = await gfApiService.GetDiscordAccountsForUser(application.UserId);
        if (!userDiscordAccountResponse.TryGetData(out var discordAccounts) || discordAccounts == null || discordAccounts.Count == 0)
        {
            await Context.Interaction.ModifyResponse([ErrorUserNotFound(userDiscordAccountResponse.ErrorMessage!)]);
            return;
        }

        var builtComponent = await applicationService.GenerateApplicationSummaryComponent(discordAccounts.First().DiscordSnowflake, application, false);
        await Context.Interaction.ModifyResponse(components: [builtComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }
    
}