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

        var users = connectionWithUsers?.Users ?? [];
        if (users.Count == 0)
        {
            var cached = accountLinkService.GetCachedVerifiedUser(Context.User.Id);
            if (cached is not null)
            {
                var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
                var accountViewComponent = await accountLinkService.GenerateAccountViewComponent(cached, channelUrl);
                await Context.Interaction.SendResponse(components: [accountViewComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
                return;
            }
        }

        var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.AccountView, users);
        
        await Context.Interaction.SendResponse(components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }
    
}