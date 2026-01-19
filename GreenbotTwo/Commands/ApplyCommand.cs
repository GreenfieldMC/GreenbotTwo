using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class ApplyCommand(IApplicationService applicationService, IGreenfieldApiService apiService, IAccountLinkService accountLinkService) : ApplicationCommandModule<ApplicationCommandContext> 
{
    
    private static readonly EmbedProperties UserErrorNoLinkedAccountsEmbed = GenericEmbeds.UserError(
        "Greenfield Application Service",
        "There seem to be no Minecraft accounts linked to your Discord profile. Please run the `/accounts` command to link your accounts together before applying!"
    );
    
    [SlashCommand("apply", "Apply to join the Greenfield team")]
    public async Task Apply()
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        //if they have an application form actively being filled out, we should return that.
        var isAppInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
        
        //if the user does not have an in-progress application, attempt to find their linked account.
        if (!isAppInProgress)
        {
            var connectionWithUsersResponse = await apiService.GetUsersConnectedToDiscordAccount(Context.User.Id);
            if (!connectionWithUsersResponse.TryGetDataNonNull(out var connectionWithUsers) || connectionWithUsers.Users.Count == 0)
            {
                await Context.Interaction.ModifyResponse([UserErrorNoLinkedAccountsEmbed]);
                return;
            }

            var users = connectionWithUsers.Users;
            var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.Application, users);
            await Context.Interaction.ModifyResponse(components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
            return;
        }
        
        var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application was expected to be in progress but could not be found.");
        await Context.Interaction.ModifyResponse([ApplyInteractions.ApplicationStartEmbed], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
    }
    
}