using System.Text;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Models;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace GreenbotTwo.Interactions;

public class ReviewInteractions
{

    public static readonly Func<string, EmbedProperties> CallbackUnableToParseAppId = (summaryTitle) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Unable to parse the application Id from application. See summary: `{summaryTitle}`");
    public static readonly Func<long, EmbedProperties> CallbackApplicationNotFound = (appIdString) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Unable to retrieve the application with Id `{appIdString}`.");
    public static readonly Func<long, string?, EmbedProperties> CallbackDiscordUserNotFound = (appId, errorMessage) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Unable to retrieve Discord user associated with application Id `{appId}`. Error: {errorMessage}");
    public static readonly Func<long, EmbedProperties> CallbackNoDiscordUserAssociated = (appId) =>
        GenericEmbeds.InternalError("Internal Application Error", $"No Discord user associated with application Id `{appId}`.");
    public static readonly Func<long, EmbedProperties> CallbackNoDiscordAccountSelected = (appId) => 
        GenericEmbeds.InternalError("Internal Application Error", $"No Discord account was selected for application Id `{appId}`.");
    public static readonly Func<long, string?, EmbedProperties> CallbackFailedToApproveApplication = (appId, errorMessage) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Failed to approve application Id `{appId}`. Error: {errorMessage}");
    public static readonly Func<long, string?, EmbedProperties> CallbackFailedToRejectApplication = (appId, errorMessage) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Failed to reject application Id `{appId}`. Error: {errorMessage}");
    public static readonly Func<long, EmbedProperties> CallbackBadRejectionComment = (appId) => 
        GenericEmbeds.UserError("Greenfield Application Service", $"No rejection reason was provided for rejecting application Id `{appId}`. Please provide at least one reason before rejecting.");
    
    public class ReviewButtonInteractions(IGreenfieldApiService gfApiService) : ComponentInteractionModule<ButtonInteractionContext>
    {
        
        [ComponentInteraction("button_does_nothing")]
        public static void DoNothingButton()
        {
            
        }
        
        [ComponentInteraction("buildapp_approve_button")]
        public async Task<InteractionCallbackProperties> ApproveApplicationButton()
        {
            
            var applicationSummaryTitle = Context.Message.Components.OfType<ComponentContainer>().First().Components.OfType<TextDisplay>().First().Content;
            var appIdString = applicationSummaryTitle.Split('#').Last().Trim();
            if (!long.TryParse(appIdString, out var appId))
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackUnableToParseAppId(appIdString)]));
            
            
            var actualApplicationResult = await gfApiService.GetApplicationById(appId);
            if (!actualApplicationResult.TryGetDataNonNull(out var application))
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackApplicationNotFound(appId)]));
            
            
            var discordUsersResult = await gfApiService.GetDiscordAccountsForUser(application.UserId);
            if (!discordUsersResult.TryGetDataNonNull(out var discordUsers))
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackDiscordUserNotFound(appId, discordUsersResult.ErrorMessage)]));

            if (discordUsers.Count == 0)
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackNoDiscordUserAssociated(appId)]));

            IEnumerable<IModalComponentProperties> modalComponents;
            var userMenuComponent = new UserMenuProperties("selected_discord_account")
                .WithPlaceholder("Select a Discord account")
                .WithDefaultValues(discordUsers.Select(u => u.DiscordSnowflake))
                .WithMaxValues(1)
                .WithMinValues(1);
            var additionalComments = new LabelProperties("Additional Comments", new TextInputProperties("additional_comments", TextInputStyle.Paragraph)
                .WithRequired(false)
                .WithPlaceholder("Optional additional comments to save to the database on approval. The user will NOT see these."));

            if (discordUsers.Count > 1)
                modalComponents =
                [
                    new TextDisplayProperties($"Multiple Discord accounts are associated with the user who submitted application ID {appId}. Please select the correct account below."),
                    new LabelProperties("Select Discord Account", userMenuComponent),
                    additionalComments
                ];
            else
                modalComponents =
                [
                    new TextDisplayProperties($"You are about to approve the application for <@{discordUsers[0]}> (Application ID: {appId})."),
                    new LabelProperties("Select Discord Account", userMenuComponent),
                    additionalComments
                ];

            return InteractionCallback.Modal(new ModalProperties("buildapp_approve_modal", "Approve Build Application").WithComponents(modalComponents));
        }

        [ComponentInteraction("buildapp_reject_button")]
        public async Task<InteractionCallbackProperties> RejectApplicationButton()
        {
            
            var applicationSummaryTitle = Context.Message.Components.OfType<ComponentContainer>().First().Components.OfType<TextDisplay>().First().Content;
            var appIdString = applicationSummaryTitle.Split('#').Last().Trim();
            if (!long.TryParse(appIdString, out var appId))
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackUnableToParseAppId(appIdString)]));
            
            
            var actualApplicationResult = await gfApiService.GetApplicationById(appId);
            if (!actualApplicationResult.TryGetDataNonNull(out var application))
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackApplicationNotFound(appId)]));
            
            
            var discordUsersResult = await gfApiService.GetDiscordAccountsForUser(application.UserId);
            if (!discordUsersResult.TryGetDataNonNull(out var discordUsers))
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackDiscordUserNotFound(appId, discordUsersResult.ErrorMessage)]));
            
            if (discordUsers.Count == 0)
                return InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([CallbackNoDiscordUserAssociated(appId)]));
            

            IEnumerable<IModalComponentProperties> baseModalComponents =
            [
                new LabelProperties("Custom Rejection Reason", new TextInputProperties("custom_rejection_reason", TextInputStyle.Paragraph).WithRequired(false)),
                new LabelProperties("Standard Rejection Reasons", new StringMenuProperties("defined_rejection_reasons")
                    .WithRequired(false)
                    .WithMinValues(0)
                    .WithMaxValues(StandardBuildAppFailureMessage.AllFailureMessages.Count())
                    .WithOptions(StandardBuildAppFailureMessage.AllFailureMessages.Select(m => m.ToSelectOption()))
                )
            ];
            
            var userMenuComponent = new UserMenuProperties("selected_discord_account")
                .WithPlaceholder("Select a Discord account")
                .WithDefaultValues(discordUsers.Select(u => u.DiscordSnowflake))
                .WithMaxValues(1)
                .WithMinValues(1);
            
            if (discordUsers.Count > 1)
            {
                var additionalComponents = new List<IModalComponentProperties>
                {
                    new TextDisplayProperties(
                        $"Multiple Discord accounts are associated with the user who submitted application ID {appId}. Please select the correct account below."),
                    new LabelProperties("Select Discord Account", userMenuComponent)
                };
                additionalComponents.AddRange(baseModalComponents);
                baseModalComponents = additionalComponents;
            }
            else
            {
                var additionalComponents = new List<IModalComponentProperties>
                {
                    new TextDisplayProperties(
                        $"You are about to reject the application for <@{discordUsers[0]}> (Application ID: {appId}). Please provide a reason for rejection below."),
                    new LabelProperties("Select Discord Account", userMenuComponent)
                };
                additionalComponents.AddRange(baseModalComponents);
                baseModalComponents = additionalComponents;
            }
            
            return InteractionCallback.Modal(new ModalProperties("buildapp_reject_modal", "Reject Build Application").WithComponents(baseModalComponents));
        }
        
    }
    
    public class ReviewModalInteractions(IApplicationService applicationService, IGreenfieldApiService gfApiService, RestClient restClient) : ComponentInteractionModule<ModalInteractionContext>
    {
        
        [ComponentInteraction("buildapp_approve_modal")]
        public async Task ApproveApplicationModal()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredModifyMessage);
            
            var applicationSummaryTitle = Context!.Interaction.Message!.Components.OfType<ComponentContainer>().First().Components.OfType<TextDisplay>().First().Content;
            
            var appIdString = applicationSummaryTitle.Split('#').Last().Trim();
            if (!long.TryParse(appIdString, out var appId))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackUnableToParseAppId(appIdString)]));
                return;
            }
            
            var actualApplicationResult = await gfApiService.GetApplicationById(appId);
            if (!actualApplicationResult.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackApplicationNotFound(appId)]));
                return;
            }

            var selectedDiscordAccounts = Context.Components.FromLabel<UserMenu>()?.SelectedValues;
            if (selectedDiscordAccounts is null || selectedDiscordAccounts.Count == 0)
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackNoDiscordAccountSelected(appId)]));
                return;
            }
            
            var selectedDiscordAccount = selectedDiscordAccounts[0];
            var comments = Context.Components.FromLabel<TextInput>()?.Value;
            
            var approveApplicationResult = await applicationService.AcceptApplication(selectedDiscordAccount.Id, application, comments);
            if (!approveApplicationResult.TryGetDataNonNull(out var acceptMessageId))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackFailedToApproveApplication(appId, approveApplicationResult.ErrorMessage)]));
                return;
            }
            
            var summaryComponent = await applicationService.BuildApplicationSummary(selectedDiscordAccount.Id, application, false, true);
            
            var messageId = Context.Interaction.Message.Id;
            var channelId = Context.Channel.Id;
            _ = restClient.ModifyMessageAsync(channelId, messageId, options =>
            {
                var componentList = summaryComponent.Components.ToList();
                componentList.Add(new ComponentSeparatorProperties());
                componentList.Add(new ActionRowProperties([
                    new ButtonProperties("button_does_nothing", "Approved!", EmojiProperties.Standard("✔️"), ButtonStyle.Success).WithDisabled(),
                    new LinkButtonProperties($"https://discord.com/channels/{Context.Guild?.Id}/{acceptMessageId}", "Go to Application")
                ]));
                summaryComponent
                    .WithComponents(componentList)
                    .WithAccentColor(ColorHelpers.Success);
                options.WithComponents([summaryComponent]);
            });
        }

        [ComponentInteraction("buildapp_reject_modal")]
        public async Task RejectApplicationModal()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredModifyMessage);

            var applicationSummaryTitle = Context!.Interaction.Message!.Components.OfType<ComponentContainer>().First().Components.OfType<TextDisplay>().First().Content;

            var appIdString = applicationSummaryTitle.Split('#').Last().Trim();
            if (!long.TryParse(appIdString, out var appId))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackUnableToParseAppId(appIdString)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var actualApplicationResult = await gfApiService.GetApplicationById(appId);
            if (!actualApplicationResult.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackApplicationNotFound(appId)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var selectedDiscordAccounts = Context.Components.FromLabel<UserMenu>()?.SelectedValues;
            if (selectedDiscordAccounts is null || selectedDiscordAccounts.Count == 0)
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackNoDiscordAccountSelected(appId)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var selectedDiscordAccount = selectedDiscordAccounts[0];

            var customRejectionReason = Context.Components.FromLabel<TextInput>()?.Value;
            var definedRejectionReasons = Context.Components.FromLabel<StringMenu>()?.SelectedValues;

            var commentResult = GetRejectionComment(customRejectionReason, definedRejectionReasons?.ToList());
            if (!commentResult.TryGetDataNonNull(out var comment))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackBadRejectionComment(appId)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var denyApplicationResult = await applicationService.DenyApplication(selectedDiscordAccount.Id, application, comment);
            if (!denyApplicationResult.TryGetDataNonNull(out var denyMessageId))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackFailedToRejectApplication(appId, denyApplicationResult.ErrorMessage)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var summaryComponent = await applicationService.BuildApplicationSummary(selectedDiscordAccount.Id, application, false, true);

            var messageId = Context.Interaction.Message.Id;
            var channelId = Context.Channel.Id;
            _ = restClient.ModifyMessageAsync(channelId, messageId, options =>
            {
                var componentList = summaryComponent.Components.ToList();
                componentList.Add(new ComponentSeparatorProperties());
                componentList.Add(new ActionRowProperties([
                    new ButtonProperties("button_does_nothing", "Rejected!", EmojiProperties.Standard("✖️"), ButtonStyle.Danger).WithDisabled(),
                    new LinkButtonProperties($"https://discord.com/channels/{Context.Guild?.Id}/{denyMessageId}", "Go to Application")
                ]));
                summaryComponent
                    .WithComponents(componentList)
                    .WithAccentColor(ColorHelpers.Failure);
                options.WithComponents([summaryComponent]);
            });
        }

        private static Result<string> GetRejectionComment(string? customReason, List<string>? definedReasons)
        {
            if (string.IsNullOrWhiteSpace(customReason) && (definedReasons is null || !definedReasons.Any()))
                return Result<string>.Failure("No rejection reason provided.");
            
            var fullRejectionReason = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(customReason))
                fullRejectionReason.AppendLine(customReason);

            if (definedReasons is null || definedReasons.Count == 0)
                return Result<string>.Success(fullRejectionReason.ToString().Trim());
            
            if (!string.IsNullOrWhiteSpace(customReason)) fullRejectionReason.AppendLine().AppendLine("Additional Rejection Reason(s):");
            else fullRejectionReason.AppendLine("Rejection Reason(s):");
            
            foreach (var reason in definedReasons)
                fullRejectionReason.AppendLine($"- {StandardBuildAppFailureMessage.FromId(reason).Message}");
            return Result<string>.Success(fullRejectionReason.ToString().Trim());
        }
        
    }
    
}