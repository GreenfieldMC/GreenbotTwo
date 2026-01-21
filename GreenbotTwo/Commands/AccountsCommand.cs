using System.Net;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class AccountsCommand(IGreenfieldApiService apiService, IAccountLinkService accountLinkService) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    private static readonly EmbedProperties FailedToFetchUsersEmbed = GenericEmbeds.InternalError(
        "AccountLink Service",
        "An internal error occurred while trying to fetch your linked accounts. Please try again later."
    );

    [SlashCommand("accounts", "View your linked accounts.")]
    public async Task Accounts()
    {
        var userResponse = await apiService.GetUsersConnectedToDiscordAccount(Context.User.Id);

        if (!userResponse.TryGetData(out var connectionWithUsers) && userResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await Context.Interaction.SendResponse([FailedToFetchUsersEmbed], MessageFlags.Ephemeral);
            return;
        }

        var component = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.AccountView, connectionWithUsers?.Users ?? []);
        
        await Context.Interaction.SendResponse(components: [component], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }
    
}