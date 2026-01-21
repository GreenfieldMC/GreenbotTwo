using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GreenbotTwo.Models.GreenfieldApi.User;

namespace GreenbotTwo.Interactions.AccountLink;

public class AccountLinkInteractions
{
    public const string AccountViewUserSelectionButton = "select_user_for_account_view";

    #region Normal Authorization Embeds

    private static readonly EmbedProperties ValidatingAuthCodeEmbed =
        GenericEmbeds.Info("AccountLink Service", "Validating your auth code...");
    private static readonly EmbedProperties ValidatingUsernameEmbed =
        GenericEmbeds.Info("AccountLink Service", "Validating your Minecraft username...");
    private static readonly EmbedProperties AccountLinkedSuccessfullyEmbed =
        GenericEmbeds.Success("AccountLink Service", "Your Minecraft account has been successfully linked!");

    #endregion

    #region User Error Embeds

    private static readonly EmbedProperties UserErrorFailedToValidateUsername = 
        GenericEmbeds.UserError("AccountLink Service", "The provided Minecraft username is either invalid, or the validation service is down. Please ensure you have entered it correctly and try again.");
    private static readonly EmbedProperties UserErrorFailedToValidateAuthCode =
        GenericEmbeds.UserError("AccountLink Service", "Failed to validate your auth code.");
    private static readonly EmbedProperties UserErrorNoLinkingInProgress =
        GenericEmbeds.UserError("AccountLink Service",
            "You do not have an account linking process in progress. Please start a new one.");

    #endregion

    #region Internal Error Embeds

    private static readonly EmbedProperties InternalErrorFailedToCreateUserAccount =
        GenericEmbeds.InternalError("AccountLink Service", "Failed to create your Minecraft account in our system.");
    public static readonly EmbedProperties InternalErrorFailedToFindUser = GenericEmbeds.InternalError("Internal Application Error",
        "We were unable to find a user associated with the selected account. Please try again with a different user.");
    
    #endregion
    
    public class AccountLinkButtonInteractions(IAccountLinkService accountLinkService) : ComponentInteractionModule<ButtonInteractionContext>
    {
        [ComponentInteraction("link_new_account")]
        public async Task<InteractionCallbackProperties> LinkNewAccount()
        {
            accountLinkService.ClearInProgressAccountLink(Context.User.Id);
            accountLinkService.GetOrStartAccountLinkForm(Context.User.Id);
            var modal = new ModalProperties("authhub_username_modal", "Minecraft Account Link")
                .WithComponents([
                    new TextDisplayProperties(
                        "We require you have direct access to a Java Edition Minecraft account. We will need to verify your account exists. What is your Minecraft username?"),
                    new LabelProperties("Minecraft Username",
                        new TextInputProperties("mc_username", TextInputStyle.Short).WithRequired())
                ]);

            return InteractionCallback.Modal(modal);
        }

        [ComponentInteraction("finish_linking_account")]
        public async Task<InteractionCallbackProperties> FinishLinkingAccount()
        {
            if (!accountLinkService.HasAccountLinkInProgress(Context.User.Id))
            {
                _ = Context.Interaction.Message.DeleteAsync();
                return InteractionCallback.Message(new InteractionMessageProperties()
                    .WithEmbeds([GenericEmbeds.UserError("Account Link Service", "You do not have an account linking process in progress. Please start a new one.")])
                    .WithFlags(MessageFlags.Ephemeral));
            }
            
            var accountLinkForm = accountLinkService.GetOrStartAccountLinkForm(Context.User.Id);
            
            var modal = new ModalProperties("authhub_code_modal", "Minecraft Account Link")
                .WithComponents([
                    new TextDisplayProperties(
                        "Please join the server `play.greenfieldmc.net` to retrieve your auth code. If you are already whitelisted, join the server and run the command `/authhub`"),
                    new TextDisplayProperties($"Your Minecraft username: `{accountLinkForm.MinecraftUsername}`"),
                    new LabelProperties("Authentication Code",
                        new TextInputProperties("mc_auth_code", TextInputStyle.Short).WithRequired())
                ]);
            return InteractionCallback.Modal(modal);
        }
        
    }

    public class AccountLinkSelectUserInteractions(IAccountLinkService accountLinkService, IGreenfieldApiService gfApiService, RestClient restClient) : ComponentInteractionModule<StringMenuInteractionContext>
    {
        [ComponentInteraction(AccountViewUserSelectionButton)]
        public async Task UserSelectionButton()
        {
            await Context.Interaction.SendModifyLaterResponse();
            
            var selectedId = Context.SelectedValues.FirstOrDefault();

            if (selectedId is null || string.IsNullOrWhiteSpace(selectedId) ||
                !long.TryParse(selectedId, out var userId) ||
                !(await gfApiService.GetUserById(userId)).TryGetDataNonNull(out var user))
            {
                _ = Context.Channel.SendMessageAsync(new MessageProperties().WithEmbeds([InternalErrorFailedToFindUser]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            var selectionComponent = await accountLinkService.GenerateAccountViewComponent(user, $"discord://discord.com/channel/{Context.Guild!.Id}/{Context.Channel.Id}");
            await Context.Interaction.ModifyResponse(components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
        }
    }
    
    public class AuthCodeModalInteractions( IAuthenticationHubService authHubService, IAccountLinkService accountLinkService, IGreenfieldApiService apiService, IMojangService mojangApi) : ComponentInteractionModule<ModalInteractionContext>
    {

        [ComponentInteraction("authhub_username_modal")]
        public async Task HandleUsernameSubmission()
        {
            var username = Context.Components.FromLabel<TextInput>("mc_username")!.Value;
            await Context.Interaction.SendResponse([ValidatingUsernameEmbed], flags: MessageFlags.Ephemeral);
            
            var validationResult = await authHubService.ValidateUsername(username);
            if (!validationResult.IsSuccessful)
            {
                await Context.Interaction.ModifyResponse([UserErrorFailedToValidateUsername]);
                return;
            }

            var mojangResult = await mojangApi.GetMinecraftProfileByUsername(username);
            if (!mojangResult.TryGetDataNonNull(out var slimProfile))
            {
                await Context.Interaction.ModifyResponse([UserErrorFailedToValidateUsername]);
                return;
            }
            
            var accountLinkForm = accountLinkService.GetOrStartAccountLinkForm(Context.User.Id);
            accountLinkForm.MinecraftUsername = username;
            accountLinkForm.MinecraftUuid = slimProfile.Uuid;

            var container = await accountLinkService.GenerateFinishLinkingComponent();

            _ = Context.Interaction.DeleteResponseAsync();
            _ = Context.Interaction.Message.DeleteAsync();
            await Context.Interaction.Channel.SendMessageAsync(new MessageProperties()
                .WithComponents([
                    container
                ])
                .WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2));
        }
        
        [ComponentInteraction("authhub_code_modal")]
        public async Task HandleAuthCodeSubmission()
        {
            await Context.Interaction.SendResponse([ValidatingAuthCodeEmbed], MessageFlags.Ephemeral);
            
            if (!accountLinkService.HasAccountLinkInProgress(Context.User.Id))
            {
                await Context.Interaction.Message!.DeleteAsync();
                await Context.Interaction.ModifyResponse([UserErrorNoLinkingInProgress]);
                return;
            }
            
            var accountLinkForm = accountLinkService.GetOrStartAccountLinkForm(Context.User.Id);

            var authCode = Context.Components.FromLabel<TextInput>("mc_auth_code")?.Value;

            var authorizeResult = await authHubService.Authorize(accountLinkForm.MinecraftUsername, authCode ?? "");
            if (authorizeResult.IsSuccessful)
            {
                User actualUser;
                var foundUserResult = await apiService.GetUserByMinecraftUuid(accountLinkForm.MinecraftUuid!.Value);
                if (!foundUserResult.TryGetDataNonNull(out var existingUser))
                {
                    var createUserResult = await apiService.CreateUser(accountLinkForm.MinecraftUuid!.Value, accountLinkForm.MinecraftUsername);
                    if (!createUserResult.TryGetDataNonNull(out var createdUser))
                    {
                        await Context.Interaction.ModifyResponse([InternalErrorFailedToCreateUserAccount]);
                        return;
                    }
                    actualUser = createdUser;
                }
                else 
                    actualUser = existingUser;
                
                _ = Context.Interaction.Message!.DeleteAsync();
                await Context.Interaction.ModifyResponse([AccountLinkedSuccessfullyEmbed]);
                _ = Context.Channel.SendMessageAsync(new MessageProperties()
                    .WithComponents([await accountLinkService.GenerateAccountViewComponent(actualUser, $"discord://discord.com/channel/{Context.Guild.Id}/{Context.Channel.Id}")])
                    .WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2));
                accountLinkService.ClearInProgressAccountLink(Context.User.Id);
                return;
            }

            await Context.Interaction.ModifyResponse([UserErrorFailedToValidateAuthCode]);
        }
        
    }
    
}