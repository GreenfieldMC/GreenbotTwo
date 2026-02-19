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
using User = NetCord.User;

namespace GreenbotTwo.Commands;

[SlashCommand("accounts", "Various account related commands.")]
public class AccountsCommand(IGreenfieldApiService apiService, IAccountLinkService accountLinkService, IMojangService mojangService) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    private static readonly EmbedProperties FailedToFetchUsersEmbed = GenericEmbeds.InternalError(
        "AccountLink Service",
        "An internal error occurred while trying to fetch your linked accounts. Please try again later."
    );
    private static readonly EmbedProperties ErrorUnknownMinecraftUser = GenericEmbeds.UserError(
        "AccountLink Service",
        "Unknown Minecraft user."
    );
    private static readonly EmbedProperties ErrorUnlinkedMinecraftUser = GenericEmbeds.UserError(
        "AccountLink Service",
        "The specified Minecraft user was not found in the Greenfield user system."
    );
    private static readonly EmbedProperties ErrorTargetHasNoLinkedAccountsEmbed = GenericEmbeds.UserError(
        "AccountLink Service",
        "The specified user does not have any accounts linked to their Discord account."
    );

    [SubSlashCommand("show", "View your linked accounts.")]
    public async Task Show()
    {
        await ShowByDiscord(Context.User);
    }

    [SubSlashCommand("show-by-discord", "View linked accounts of a specific Discord user.")]
    public async Task ShowByDiscord(
        [UserRequiresAnyRoleOrSelf<AccountCommandSettings, User>("RolesThatCanViewOtherUserAccounts")]
        [SlashCommandParameter(Description = "The Discord user to view accounts for.")] User user)
    {
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var userResponse = await apiService.GetUsersConnectedToDiscordAccount(user.Id);

        if (!userResponse.TryGetData(out var connectionWithUsers) && userResponse.StatusCode != HttpStatusCode.NotFound)
        {
            await Context.Interaction.ModifyResponse([FailedToFetchUsersEmbed]);
            return;
        }

        var users = connectionWithUsers?.Users ?? [];
        if (users.Count == 0)
        {
            var cached = accountLinkService.GetCachedVerifiedUser(user.Id);
            if (cached is not null)
            {
                var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
                var accountViewComponent = await accountLinkService.GenerateAccountViewComponent(cached, channelUrl);
                await Context.Interaction.ModifyResponse(embeds: null, components: [accountViewComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
                return;
            }
        }

        var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.AccountView, users);
        
        await Context.Interaction.ModifyResponse(embeds: null, components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }

    [SubSlashCommand("show-by-minecraft", "View linked accounts of a specific Minecraft user.")]
    public async Task ShowByMinecraft(
        [UserRequiresAnyRoleOrSelf<AccountCommandSettings, string>("RolesThatCanViewOtherUserAccounts")]
        [SlashCommandParameter(Description = "The Minecraft username to view accounts for.")] string minecraftUsername)
    {
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var mojangUserResult = await mojangService.GetMinecraftProfileByUsername(minecraftUsername);
        if (!mojangUserResult.TryGetDataNonNull(out var mojangUser))
        {
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnknownMinecraftUser]);
            return;
        }

        var gfUserResult = await apiService.GetUserByMinecraftUuid(mojangUser.Uuid);
        if (!gfUserResult.TryGetDataNonNull(out var gfUser))
        {
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnlinkedMinecraftUser]);
            return;
        }

        var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
        var accountViewComponent = await accountLinkService.GenerateAccountViewComponent(gfUser, channelUrl);
        await Context.Interaction.ModifyResponse(embeds: null, components: [accountViewComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }
    
}