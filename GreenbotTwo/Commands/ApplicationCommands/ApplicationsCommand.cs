using GreenbotTwo.Configuration.Models.CommandPermissions.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Models.GreenfieldApi;
using GreenbotTwo.NetCordSupport.Preconditions;
using GreenbotTwo.NetCordSupport.TypeReaders;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;
using User = NetCord.User;

namespace GreenbotTwo.Commands.ApplicationCommands;

[SlashCommand("applications", "Various application related commands.")]
public class ApplicationsCommand(IGreenfieldApiService gfApiService, IMojangService mojangService, IApplicationService applicationService, ILogger<IApplicationCommandContext> commandLogger) : ApplicationCommandModule<ApplicationCommandContext>
{
    
    private static readonly EmbedProperties ErrorCouldNotFetchApplications = GenericEmbeds.InternalError("Applications Error", "An error occurred while trying to fetch applications. Please try again later.");
    private static readonly EmbedProperties ErrorNoApplications = GenericEmbeds.UserError("Applications Error", "No applications found for the specified user.");
    private static readonly EmbedProperties ErrorUnknownMinecraftUser = GenericEmbeds.UserError("Applications Error", "Unknown Minecraft user.");
    private static readonly EmbedProperties ErrorUnlinkedMinecraftUser = GenericEmbeds.UserError("Applications Error", "The specified Minecraft user was not found in the Greenfield user system.");
    private static readonly EmbedProperties ErrorUnknownApplicationUser = GenericEmbeds.UserError("Applications Error", "The user associated with this application could not be found.");

    [SubSlashCommand("list", "List your applications.")]
    public async Task ListApplications()
    {
        await ListApplicationsOfDiscordUser(Context.User);
    }
    
    /*
     * /applications list -> list the current user's applications
     * /applications list @discordUser -> open dialog to select which linked Minecraft user to list applications for (if linked)
     * /applications list minecraftUser -> list the applications of a Minecraft user (if linked)
     * /applications view applicationId -> view an application by its ID (if the user has permission to view it)
     */
    
    [SubSlashCommand("list-by-discord", "List all applications of a specific Discord user.")]
    public async Task ListApplicationsOfDiscordUser(
        [UserRequiresAnyRoleOrSelf<ApplicationCommandSettings, User>("RolesThatCanListOtherUserApps")]
        [SlashCommandParameter(Description = "The Discord user to list applications for.")] User user)
    {
        commandLogger.LogCommandExecution(Context, $"user: {user.Username}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var gfUserResult = await gfApiService.GetUsersConnectedToDiscordAccount(user.Id);
        if (!gfUserResult.TryGetDataNonNull(out var discordConnection) || discordConnection.Users.Count == 0)
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch users for user {user.Username} ({user.Id}). Status code: {gfUserResult.StatusCode}, Error: {gfUserResult.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnlinkedMinecraftUser]);
            return;
        }
        
        var embeds = new List<EmbedProperties>();
        
        foreach (var linkedUser in discordConnection.Users)
        {
            var appListResult = await gfApiService.GetApplicationsByUser(linkedUser.UserId);
            if (!appListResult.TryGetDataNonNull(out var applicationsEnum))
            {
                commandLogger.LogCommandDebug(Context, $"Failed to fetch applications for user {linkedUser.Username} ({linkedUser.UserId}). Status code: {appListResult.StatusCode}, Error: {appListResult.ErrorMessage}");
                continue;
            }
            
            var appList = applicationsEnum.ToList();
            if (appList.Count <= 0)
            {
                commandLogger.LogCommandDebug(Context, $"No applications found for user {linkedUser.Username} ({linkedUser.UserId}).");
                continue;
            }
            
            commandLogger.LogCommandDebug(Context, $"Found {appList.Count} applications for user {linkedUser.Username} ({linkedUser.UserId}).");
            var embed = GenerateApplicationListEmbed(linkedUser.Username, appList);
            embeds.Add(embed);
        }
        
        if (embeds.Count == 0)
        {
            await Context.Interaction.ModifyResponse(embeds: [ErrorNoApplications]);
            return;
        }
        
        //only up to 5 embeds per message, if there are more than 5, they will need to be split into multiple messages as follow up messages
        var embedsToSend = embeds.Take(5).ToArray();
        await Context.Interaction.ModifyResponse(embeds: embedsToSend);
        var remainingEmbeds = embeds.Skip(5).ToArray();
        while (remainingEmbeds.Length > 0)        {
            var embedsForThisMessage = remainingEmbeds.Take(5).ToArray();
            await Context.Interaction.SendFollowupResponse(embeds: embedsForThisMessage, flags: MessageFlags.Ephemeral);
            remainingEmbeds = remainingEmbeds.Skip(5).ToArray();
        }
    }

    [SubSlashCommand("list-by-minecraft", "List all applications of a specific Minecraft user.")]
    public async Task ListApplicationsOfMinecraftUser(
        [UserRequiresAnyRoleOrSelf<ApplicationCommandSettings, string>("RolesThatCanListOtherUserApps")] 
        [SlashCommandParameter(Name = "minecraft_username", Description = "The Minecraft username to list applications for.")]
        string minecraftUsername)
    {
        commandLogger.LogCommandExecution(Context, $"minecraft_username: {minecraftUsername}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var mojangUserResult = await mojangService.GetMinecraftProfileByUsername(minecraftUsername);
        if (!mojangUserResult.TryGetDataNonNull(out var mojangUser))
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch Minecraft profile for username {minecraftUsername}. Status code: {mojangUserResult.StatusCode}, Error: {mojangUserResult.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnknownMinecraftUser]);
            return;
        }

        var gfUserResult = await gfApiService.GetUserByMinecraftUuid(mojangUser.Uuid);
        if (!gfUserResult.TryGetDataNonNull(out var gfUser))
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch Greenfield user for Minecraft UUID {mojangUser.Uuid} (username: {minecraftUsername}). Status code: {gfUserResult.StatusCode}, Error: {gfUserResult.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnlinkedMinecraftUser]);
            return;
        }
        
        var appListResult = await gfApiService.GetApplicationsByUser(gfUser.UserId);
        if (!appListResult.TryGetDataNonNull(out var applicationsEnum))
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch applications for Minecraft user {minecraftUsername} (UUID: {mojangUser.Uuid}, UserId: {gfUser.UserId}). Status code: {appListResult.StatusCode}, Error: {appListResult.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorCouldNotFetchApplications]);
            return;
        }
        
        var appList = applicationsEnum.ToList();
        if (appList.Count == 0)
        {
            commandLogger.LogCommandDebug(Context, $"No applications found for Minecraft user {minecraftUsername} (UUID: {mojangUser.Uuid}, UserId: {gfUser.UserId}).");
            await Context.Interaction.ModifyResponse(embeds: [ErrorNoApplications]);
            return;
        }
        
        commandLogger.LogCommandDebug(Context, $"Applications found for Minecraft user {minecraftUsername} (UUID: {mojangUser.Uuid}, UserId: {gfUser.UserId}). Count: {appList.Count}");
        
        var embed = GenerateApplicationListEmbed(minecraftUsername, appList);
        await Context.Interaction.ModifyResponse(embeds: [embed]);
    }

    private static EmbedProperties GenerateApplicationListEmbed(string minecraftUsername,
        List<LatestApplicationStatus> appList)
    {
        return GenericEmbeds.Info("Greenfield Application Service", $"Applications found for Minecraft User `{minecraftUsername}`. To view more details about any application, use the command `/applications view <application_id>`")
            .WithFields(appList.OrderByDescending(a => a.LatestStatus?.CreatedOn).Take(24).Select(app => new EmbedFieldProperties().WithInline().WithName($"App #{app.ApplicationId}").WithValue($"`{app.LatestStatus?.Status ?? "No status history found."}`")));
    }
    
    [SubSlashCommand("view", "View an application by its ID.")]
    public async Task ViewApplicationById(
        [SlashCommandParameter(Name = "application_id", Description = "The ID of the application to view.", TypeReaderType = typeof(ApplicationTypeReader<ApplicationCommandContext>))] 
        [ApplicationRequiresAnyRoleOrOwner<ApplicationCommandSettings>("RolesThatCanViewOtherUserApps")]
        Application application) 
    {
        commandLogger.LogCommandExecution(Context, $"application_id: {application.ApplicationId}");
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var userDiscordAccountResponse = await gfApiService.GetDiscordAccountsForUser(application.UserId);
        if (!userDiscordAccountResponse.TryGetData(out var discordAccounts) || discordAccounts == null || discordAccounts.Count == 0)
        {
            commandLogger.LogCommandDebug(Context, $"Failed to fetch Discord accounts for user ID {application.UserId} associated with application ID {application.ApplicationId}. Status code: {userDiscordAccountResponse.StatusCode}, Error: {userDiscordAccountResponse.ErrorMessage}");
            await Context.Interaction.ModifyResponse(embeds: [ErrorUnknownApplicationUser]);
            return;
        }
        
        var builtComponent = await applicationService.GenerateApplicationSummaryComponent(discordAccounts.First().DiscordSnowflake, application, includeButtons: false);
        var latestStatus = application.BuildAppStatuses.OrderByDescending(s => s.CreatedOn).FirstOrDefault();
        EmbedProperties? statusEmbed = null;
        if (latestStatus?.StatusMessage != null && !latestStatus.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            statusEmbed = new EmbedProperties()
                .WithTitle($"Application #{application.ApplicationId} - Status: {latestStatus.Status}")
                .WithDescription(latestStatus.StatusMessage);
        }
        
        commandLogger.LogCommandDebug(Context, $"Successfully fetched Discord accounts for user ID {application.UserId} associated with application ID {application.ApplicationId}.");        
        
        await Context.Interaction.ModifyResponse(components: [builtComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2, embeds: null);
        if (statusEmbed != null)
        {
            await Context.Interaction.SendFollowupResponse(embeds: [statusEmbed], flags: MessageFlags.Ephemeral);
        }
    }
    
}