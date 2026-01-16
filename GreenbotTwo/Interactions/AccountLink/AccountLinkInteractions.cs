using System.Net;
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
    
    private static readonly EmbedProperties FailedToValidateMinecraftUsernameEmbed = GenericEmbeds.UserError(
        "AccountLink Service",
        "The provided Minecraft username is either invalid, or the validation service is down. Please ensure you have entered it correctly and try again."
    );
    
    private static readonly EmbedProperties ValidatingAuthCodeEmbed =
        GenericEmbeds.Info("AccountLink Service", "Validating your auth code...");
    
    private static readonly EmbedProperties ValidatingUsernameEmbed =
        GenericEmbeds.Info("AccountLink Service", "Validating your Minecraft username...");

    private static readonly EmbedProperties AuthorizationFailedEmbed =
        GenericEmbeds.UserError("AccountLink Service", "Failed to validate your auth code.");
    
    private static readonly EmbedProperties ApplicationsFetchFailedEmbed =
        GenericEmbeds.UserError("AccountLink Service", "Failed to fetch your applications.");
    
    private static readonly EmbedProperties FailedToFetchUserEmbed =
        GenericEmbeds.InternalError("AccountLink Service", "Failed to fetch your Minecraft account information.");
    
    private static readonly EmbedProperties FailedToCreateUserEmbed =
        GenericEmbeds.InternalError("AccountLink Service", "Failed to create your Minecraft account in our system.");
    
    private static readonly EmbedProperties AccountLinkedSuccessfullyEmbed =
        GenericEmbeds.Success("AccountLink Service", "Your Minecraft account has been successfully linked!");
    
    public static readonly EmbedProperties FailedToDetermineLinkedAccountsEmbed = GenericEmbeds.InternalError("Internal Application Error",
        "An internal error occurred while trying to determine if your Minecraft account is already linked to another Discord account. Please try again later.");
    public static readonly EmbedProperties UserNotFound = GenericEmbeds.InternalError("Internal Application Error",
        "We were unable to find a user associated with the selected account. Please try again with a different user.");
    
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
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredModifyMessage);
            
            var selectedId = Context.SelectedValues.FirstOrDefault();

            // _ = Context.Message.DeleteAsync();
            if (selectedId is null || string.IsNullOrWhiteSpace(selectedId) ||
                !long.TryParse(selectedId, out var userId) ||
                !(await gfApiService.GetUserById(userId)).TryGetDataNonNull(out var user))
            {
                _ = Context.Channel.SendMessageAsync(new MessageProperties().WithEmbeds([UserNotFound]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            var selectionComponent = await accountLinkService.GenerateAccountViewComponent(user, $"discord://discord.com/channel/{Context.Guild.Id}/{Context.Channel.Id}");
            await Context.Interaction.ModifyResponseAsync(options => options
                .WithComponents([selectionComponent])
                .WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2)
            );
        }
    }
    
    public class AuthCodeModalInteractions( IAuthenticationHubService authHubService, IAccountLinkService accountLinkService, IGreenfieldApiService apiService, IMojangService mojangApi) : ComponentInteractionModule<ModalInteractionContext>
    {

        [ComponentInteraction("authhub_username_modal")]
        public async Task HandleUsernameSubmission()
        {
            var username = Context.Components.FromLabel<TextInput>("mc_username")!.Value;
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([ValidatingUsernameEmbed]).WithFlags(MessageFlags.Ephemeral)));
            
            var validationResult = await authHubService.ValidateUsername(username);
            if (!validationResult.IsSuccessful)
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithEmbeds([FailedToValidateMinecraftUsernameEmbed]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var mojangResult = await mojangApi.GetMinecraftProfileByUsername(username);
            if (!mojangResult.TryGetDataNonNull(out var slimProfile))
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithEmbeds([FailedToValidateMinecraftUsernameEmbed]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            var accountLinkForm = accountLinkService.GetOrStartAccountLinkForm(Context.User.Id);
            accountLinkForm.MinecraftUsername = username;
            accountLinkForm.MinecraftUuid = slimProfile.Uuid;

            var container = await accountLinkService.GenerateFinishLinkingComponent();

            _ = Context.Interaction.DeleteResponseAsync();
            _ = Context.Interaction.Message.DeleteAsync();
            _ = Context.Interaction.Channel.SendMessageAsync(new MessageProperties()
                .WithComponents([
                    container
                ])
                .WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2));
        }
        
        [ComponentInteraction("authhub_code_modal")]
        public async Task HandleAuthCodeSubmission()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([ValidatingAuthCodeEmbed]).WithFlags(MessageFlags.Ephemeral)));
            
            if (!accountLinkService.HasAccountLinkInProgress(Context.User.Id))
            {
                _ = Context.Interaction.Message!.DeleteAsync();
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithEmbeds([GenericEmbeds.UserError("Account Link Service", "You do not have an account linking process in progress. Please start a new one.")])
                    );
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
                        _ = Context.Interaction.ModifyResponseAsync(options => options
                            .WithEmbeds([FailedToCreateUserEmbed])
                            .WithFlags(MessageFlags.Ephemeral));
                        return;
                    }
                    actualUser = createdUser;
                }
                else 
                    actualUser = existingUser;
                
                _ = Context.Interaction.Message!.DeleteAsync();
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithEmbeds([AccountLinkedSuccessfullyEmbed])
                    .WithFlags(MessageFlags.Ephemeral));
                _ = Context.Channel.SendMessageAsync(new MessageProperties()
                    .WithComponents([await accountLinkService.GenerateAccountViewComponent(actualUser, $"discord://discord.com/channel/{Context.Guild.Id}/{Context.Channel.Id}")])
                    .WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2));
                accountLinkService.ClearInProgressAccountLink(Context.User.Id);
                return;
            }
            
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([AuthorizationFailedEmbed])
                .WithFlags(MessageFlags.Ephemeral));
        }
        
    }
    
}