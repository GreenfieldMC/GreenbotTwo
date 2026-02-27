using System.Net;
using GreenbotTwo.Configuration.Models.CommandPermissions.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.NetCordSupport.Preconditions;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using User = NetCord.User;

namespace GreenbotTwo.Commands;

[SlashCommand("accounts", "Various account related commands.")]
public class AccountsCommand(IGreenfieldApiService apiService, IAccountLinkService accountLinkService, IMojangService mojangService, ILogger<IApplicationCommandContext> commandLogger) : ApplicationCommandModule<ApplicationCommandContext>
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
        commandLogger.LogCommandExecution(Context, $"user: {user.Username}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var userResponse = await apiService.GetUsersConnectedToDiscordAccount(user.Id);

        if (!userResponse.TryGetData(out var connectionWithUsers) && userResponse.StatusCode != HttpStatusCode.NotFound)
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch users for user {user.Username} ({user.Id}). Status code: {userResponse.StatusCode}, Error: {userResponse.ErrorMessage}");
            await Context.Interaction.ModifyResponse([FailedToFetchUsersEmbed]);
            return;
        }

        var users = connectionWithUsers?.Users ?? [];
        if (users.Count == 0)
        {
            commandLogger.LogCommandDebug(Context, $"No users found for {user.Username} ({user.Id}).");
            var cached = accountLinkService.GetCachedVerifiedUser(user.Id);
            if (cached is not null)
            {
                commandLogger.LogCommandDebug(Context, $"Using cached verified user for {user.Username} ({user.Id}).");
                var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
                var accountViewComponent = await accountLinkService.GenerateAccountViewComponent(cached, channelUrl);
                await Context.Interaction.ModifyResponse(embeds: null, components: [accountViewComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
                return;
            }
        }

        commandLogger.LogCommandDebug(Context, $"Found {users.Count} users for {user.Username} ({user.Id}).");
        var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.AccountView, users);
        
        await Context.Interaction.ModifyResponse(embeds: null, components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }

    [SubSlashCommand("show-by-minecraft", "View linked accounts of a specific Minecraft user.")]
    public async Task ShowByMinecraft(
        [UserRequiresAnyRoleOrSelf<AccountCommandSettings, string>("RolesThatCanViewOtherUserAccounts")]
        [SlashCommandParameter(Name = "minecraft_username", Description = "The Minecraft username to view accounts for.")] string minecraftUsername)
    {
        commandLogger.LogCommandExecution(Context, $"minecraft_username: {minecraftUsername}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var mojangUserResult = await mojangService.GetMinecraftProfileByUsername(minecraftUsername);
        if (!mojangUserResult.TryGetDataNonNull(out var mojangUser))
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch Minecraft user for username {minecraftUsername}. Status code: {mojangUserResult.StatusCode}, Error: {mojangUserResult.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnknownMinecraftUser]);
            return;
        }

        var gfUserResult = await apiService.GetUserByMinecraftUuid(mojangUser.Uuid);
        if (!gfUserResult.TryGetDataNonNull(out var gfUser))
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch Greenfield user for Minecraft UUID {mojangUser.Uuid}. Status code: {gfUserResult.StatusCode}, Error: {gfUserResult.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnlinkedMinecraftUser]);
            return;
        }

        commandLogger.LogCommandDebug(Context, $"Found Greenfield user for Minecraft UUID {mojangUser.Uuid}: {gfUser.Username} ({gfUser.UserId})");
        var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
        var accountViewComponent = await accountLinkService.GenerateAccountViewComponent(gfUser, channelUrl);
        await Context.Interaction.ModifyResponse(embeds: null, components: [accountViewComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
    }
    
}