using GreenbotTwo.Embeds;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class LinkAccountCommand(IGreenfieldApiService apiService, IAuthenticationHubService authHubService, RestClient restClient) : ApplicationCommandModule<ApplicationCommandContext>
{

    private static readonly EmbedProperties FailedToValidateMinecraftUsernameEmbed = GenericEmbeds.UserError(
        "AccountLink Service",
        "The provided Minecraft username is either invalid, or the validation service is down. Please ensure you have entered it correctly and try again."
    );
    
    [SlashCommand("linkaccount", "Link your Discord account to your Minecraft account.")]
    public async Task LinkAccount([SlashCommandParameter(Name = "minecraft_username", Description = "Your Minecraft Username")] string minecraftUsername)
    {
        var validationResult = await authHubService.ValidateUsername(minecraftUsername);
        if (!validationResult.IsSuccessful)
        {
            _ = Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithEmbeds([FailedToValidateMinecraftUsernameEmbed])
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        var modal = new ModalProperties("authhub_code_modal", "Authorize Minecraft")
            .WithComponents([
                new TextDisplayProperties(minecraftUsername),
                new TextDisplayProperties("Please join the server `play.greenfieldmc.net` to retrieve your auth code. If you are already whitelisted, join the server and run the command `/authhub`"),
                new LabelProperties("Auth Code", new TextInputProperties("mc_auth_code", TextInputStyle.Short).WithRequired())
            ]);
        
        _ = Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }
    
}