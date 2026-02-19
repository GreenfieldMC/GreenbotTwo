using System.Net;
using GreenbotTwo.Configuration.Models.CommandPermissions.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.NetCordSupport.Preconditions;
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
    private static readonly EmbedProperties ErrorTargetHasNoLinkedAccountsEmbed = GenericEmbeds.UserError(
        "AccountLink Service",
        "The specified user does not have any accounts linked to their Discord account."
    );

    [SlashCommand("accounts", "View your linked accounts.")]
    public async Task Accounts([UserRequiresAnyRoleOrSelf<AccountCommandSettings, User>("RolesThatCanViewOtherUserAccounts")] User? user = null)
    {
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var targetUser = user ?? Context.User;
        
        var userResponse = await apiService.GetUsersConnectedToDiscordAccount(targetUser.Id);

        if (!userResponse.TryGetData(out var connectionWithUsers) && userResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await Context.Interaction.ModifyResponse([FailedToFetchUsersEmbed]);
            return;
        }

        var users = connectionWithUsers?.Users ?? [];
        if (users.Count == 0)
        {
            var cached = accountLinkService.GetCachedVerifiedUser(targetUser.Id);
            if (cached is not null)
            {
                var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
                var accountViewComponent = await accountLinkService.GenerateAccountViewComponent(cached, channelUrl);
                await Context.Interaction.ModifyResponse(embeds: null, components: [accountViewComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
                return;
            }

            if (targetUser.Id != Context.User.Id)
            {
                await Context.Interaction.ModifyResponse([ErrorTargetHasNoLinkedAccountsEmbed]);
                return;
            }
        }

        var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.AccountView, users);
        
        await Context.Interaction.ModifyResponse(embeds: null, components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }
    
}