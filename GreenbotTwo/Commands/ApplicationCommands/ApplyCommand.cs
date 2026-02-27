using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands.ApplicationCommands;

[SlashCommand("apply", "Apply to join the Greenfield team")]
public class ApplyCommand(IApplicationService applicationService, IGreenfieldApiService apiService, IAccountLinkService accountLinkService, ILogger<IApplicationCommandContext> commandLogger) : ApplicationCommandModule<ApplicationCommandContext> 
{
    
    private static readonly EmbedProperties InternalErrorGettingLinkedUsers = GenericEmbeds.InternalError("Greenfield Application Service", "An internal error occurred while trying to fetch your linked accounts. Please try again later.");
    private static readonly EmbedProperties NoApplicationInProgress = GenericEmbeds.UserError("Greenfield Application Service", "You do not have an application in progress to cancel.");
    private static readonly EmbedProperties ApplicationAlreadySubmitted = GenericEmbeds.UserError("Greenfield Application Service", "Your application has already been submitted and cannot be cancelled.");
    private static readonly EmbedProperties ApplicationCancelled = GenericEmbeds.Success("Greenfield Application Service", "Your in-progress application has been cancelled.");
    
    [SubSlashCommand("start", "Start a new application to join the Greenfield team")]
    public async Task Apply()
    {
        commandLogger.LogCommandExecution(Context);
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);

        //if they have an application form actively being filled out, we should return that.
        var isAppInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
        
        //if the user does not have an in-progress application, attempt to find their linked account.
        if (!isAppInProgress)
        {
            commandLogger.LogCommandDebug(Context, $"No in-progress application found for user {Context.User.Username} ({Context.User.Id}). Attempting to find linked accounts.");
            var connectionWithUsersResponse = await apiService.GetUsersConnectedToDiscordAccount(Context.User.Id);
            if (!connectionWithUsersResponse.TryGetData(out var connectionWithUsers) && connectionWithUsersResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                commandLogger.LogCommandError(Context, $"Failed to fetch linked accounts for user {Context.User.Username} ({Context.User.Id}). Status code: {connectionWithUsersResponse.StatusCode}, Error: {connectionWithUsersResponse.ErrorMessage}");
                await Context.Interaction.ModifyResponse([InternalErrorGettingLinkedUsers]);
                return;
            }

            var users = connectionWithUsers?.Users ?? [];
            if (users.Count == 0)
            {
                commandLogger.LogCommandDebug(Context, $"No linked accounts found for user {Context.User.Username} ({Context.User.Id}). Checking cache for verified users.");
                var cached = accountLinkService.GetCachedVerifiedUser(Context.User.Id);
                if (cached is not null)
                    users.Add(cached);
            }
            
            commandLogger.LogCommandDebug(Context, $"Found {users.Count} linked accounts for user {Context.User.Username} ({Context.User.Id}).");
            
            var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.Application, users);
            await Context.Interaction.ModifyResponse(components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
            return;
        }
        
        commandLogger.LogCommandDebug(Context, $"Resuming in-progress application for user {Context.User.Username} ({Context.User.Id}).");
        var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application was expected to be in progress but could not be found.");
        await Context.Interaction.ModifyResponse([ApplyInteractions.ApplicationStartEmbed], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
    }
    
    [SubSlashCommand("cancel", "Cancel your current in-progress application")]
    public async Task Cancel()
    {
        commandLogger.LogCommandExecution(Context);
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        var application = applicationService.GetApplication(Context.User.Id);
        if (application is null)
        {
            commandLogger.LogCommandDebug(Context, $"User {Context.User.Username} ({Context.User.Id}) attempted to cancel an application, but none was found.");
            await Context.Interaction.ModifyResponse([NoApplicationInProgress]);
            return;
        }
        
        if (application.Submitted)
        {
            commandLogger.LogCommandDebug(Context, $"User {Context.User.Username} ({Context.User.Id}) attempted to cancel a submitted application.");
            await Context.Interaction.ModifyResponse([ApplicationAlreadySubmitted]);
            return;
        }
        
        applicationService.ClearInProgressApplication(Context.User.Id);
        commandLogger.LogCommandDebug(Context, $"User {Context.User.Username} ({Context.User.Id}) cancelled their in-progress application.");
        await Context.Interaction.ModifyResponse([ApplicationCancelled]);
    }
    
}