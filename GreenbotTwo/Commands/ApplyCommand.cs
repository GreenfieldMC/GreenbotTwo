using GreenbotTwo.Embeds;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Models;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class ApplyCommand(IApplicationService<BuilderApplicationForm, BuilderApplication> applicationService) : ApplicationCommandModule<ApplicationCommandContext> 
{

    [SlashCommand("apply", "Apply to join the Greenfield team")]
    public async Task Apply()
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        
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