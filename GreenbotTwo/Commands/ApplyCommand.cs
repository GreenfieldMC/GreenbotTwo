using GreenbotTwo.Embeds;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;

namespace GreenbotTwo.Commands;

public class ApplyCommand(IApplicationService applicationService, IGreenfieldApiService apiService, IAccountLinkService accountLinkService) : ApplicationCommandModule<ApplicationCommandContext> 
{

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
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithEmbeds([GenericEmbeds.UserError("Greenfield Application Service", "There seem to be no Minecraft accounts linked to your Discord profile. Please run the `/accounts` command to link your accounts together before applying!")])
                    .WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var users = connectionWithUsers.Users;
            // var selectionComponent = await applicationService.BuildUserSelectionComponent(Context.User.Id, users);
            var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.Application, users);
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithComponents([selectionComponent])
                .WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2));
            return;
        }
        
        var appResponse = await applicationService.GetOrStartApplication(Context.User.Id);
        if (!appResponse.TryGetDataNonNull(out var app))
        {
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([GenericEmbeds.UserError("Greenfield Application Service", appResponse.ErrorMessage ?? "An unknown error occurred while starting your application.")])
                .WithFlags(MessageFlags.Ephemeral));
            return;
        }
        
        _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([ApplyInteractions.ApplicationStartEmbed])
                .WithComponents([new ActionRowProperties().WithComponents(app.GenerateButtonsForApplication())])
                .WithFlags(MessageFlags.Ephemeral)
        );
    }
    
}