using System.Text;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Models;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
// ReSharper disable UnusedMember.Global

namespace GreenbotTwo.Interactions.BuildApplications;

public class ReviewInteractions
{

    #region InternalErrors

    private static readonly Func<long, EmbedProperties> CallbackApplicationNotFound = (appIdString) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Unable to retrieve the application with Id `{appIdString}`.");
    private static readonly Func<long, string?, EmbedProperties> CallbackFailedToApproveApplication = (appId, errorMessage) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Failed to approve application Id `{appId}`. Error: {errorMessage}");
    private static readonly Func<long, string?, EmbedProperties> CallbackFailedToRejectApplication = (appId, errorMessage) => 
        GenericEmbeds.InternalError("Internal Application Error", $"Failed to reject application Id `{appId}`. Error: {errorMessage}");
    
    #endregion

    #region UserErrors
    
    private static readonly Func<long, EmbedProperties> CallbackBadRejectionComment = (appId) => 
        GenericEmbeds.UserError("Greenfield Application Service", $"No rejection reason was provided for rejecting application Id `{appId}`. Please provide at least one reason before rejecting.");

    #endregion
    
    public class ReviewButtonInteractions(IGreenfieldApiService apiService, IApplicationService applicationService, IOptions<BuilderApplicationSettings> options) : ComponentInteractionModule<ButtonInteractionContext>
    {
        
        [ComponentInteraction("button_does_nothing")]
        public static void DoNothingButton()
        {
            
        }
        
        [ComponentInteraction("buildapp_approve_button")]
        public InteractionCallbackProperties ApproveApplicationButton(long appId, ulong discordUserId)
        {
            return InteractionCallback.Modal(new ModalProperties($"buildapp_approve_modal:{appId}:{discordUserId}", "Approve Build Application").WithComponents( [
                new TextDisplayProperties($"You are about to approve the application for {discordUserId.Mention()} (Application ID: {appId})."),
                new LabelProperties("Additional Comments", new TextInputProperties("additional_comments", TextInputStyle.Paragraph)
                    .WithRequired(false)
                    .WithPlaceholder("Optional additional comments to save to the database on approval. The user will NOT see these."))
            ]));
        }

        [ComponentInteraction("buildapp_reject_button")]
        public InteractionCallbackProperties RejectApplicationButton(long appId, ulong discordUserId)
        {
            return InteractionCallback.Modal(new ModalProperties($"buildapp_reject_modal:{appId}:{discordUserId}", "Reject Build Application").WithComponents([
                new TextDisplayProperties($"You are about to reject the application for {discordUserId.Mention()} (Application ID: {appId}). Please provide a reason for rejection below."),
                new LabelProperties("Custom Rejection Reason", new TextInputProperties("custom_rejection_reason", TextInputStyle.Paragraph).WithRequired(false).WithMaxLength(1024)),
                new LabelProperties("Standard Rejection Reasons", new CheckboxGroupProperties("defined_rejection_reasons")
                    .WithRequired(false)
                    .WithMinValues(0)
                    .WithMaxValues(StandardBuildAppFailureMessage.AllFailureMessages.Count())
                    .WithOptions(StandardBuildAppFailureMessage.AllFailureMessages.Select(m => m.ToCheckboxOption()))
                )
            ]));
        }
        
        /// <summary>
        /// Refreshes the application summary component for the given application Id. This is used when the application has been updated and the reviewer needs the component updated.
        /// </summary>
        /// <param name="appId">Applicaiton being refreshed</param>
        /// <param name="discordUserId">User who sent the application in</param>
        /// <param name="refreshMode">The refresh mode. If null, it will attempt to resolve the fresh mode based on the latest application status.</param>
        /// <returns></returns>
        [ComponentInteraction("buildapp_refresh_button")]
        public async Task<InteractionCallbackProperties> RefreshApplicationSummaryButton(long appId, ulong discordUserId, RefreshButtonMode refreshMode)
        {
            var applicationResult = await apiService.GetApplicationById(appId);
            if (!applicationResult.TryGetDataNonNull(out var application))
            {
                return InteractionCallback.Message(new InteractionMessageProperties()
                    .WithEmbeds([CallbackApplicationNotFound(appId)])
                    .WithFlags(MessageFlags.Ephemeral));
            }

            ComponentContainerProperties summaryComponent;

            if (refreshMode == RefreshButtonMode.FullSummary)
            {
                summaryComponent = await applicationService.GenerateApplicationSummaryComponent(discordUserId, application);
                summaryComponent
                    .WithComponents([
                        ..summaryComponent.Components.ToList(),
                        new ActionRowProperties([
                            new ButtonProperties($"buildapp_refresh_button:{appId}:{discordUserId}:{(int)RefreshButtonMode.FullSummary}", "Refresh", ButtonStyle.Secondary)
                        ])
                    ])
                    .WithAccentColor(ColorHelpers.Info);
            }
            else //resolve summary
            {
                if (application.IsApproved || application.IsRejected)
                {
                    var forwardedChannelId = application.IsApproved
                        ? await applicationService.AcceptApplication(discordUserId, application, application.LatestStatus?.StatusMessage, false)
                        : await applicationService.DenyApplication(discordUserId, application, application.LatestStatus?.StatusMessage ?? "No reason provided.", false);
                    summaryComponent = await applicationService.GenerateApplicationSummaryComponent(discordUserId, application, onlyShowBasicInfo: true);
                    summaryComponent
                        .WithAccentColor(application.IsApproved ? ColorHelpers.Success : ColorHelpers.Failure)
                        .WithComponents([
                            ..summaryComponent.Components.ToList(),
                            new ActionRowProperties([
                                application.IsApproved 
                                    ? new ButtonProperties("button_does_nothing", "Approved!", EmojiProperties.Standard("✔️"), ButtonStyle.Success).WithDisabled() 
                                    : new ButtonProperties("button_does_nothing", "Rejected!", EmojiProperties.Standard("✖️"), ButtonStyle.Danger).WithDisabled(),
                                new LinkButtonProperties($"https://discord.com/channels/{Context.Guild?.Id}/{forwardedChannelId.GetNonNullOrThrow()}", "Go to Application")
                            ])
                        ]);
                }
                else
                {
                    summaryComponent = (await applicationService.GenerateApplicationSummaryComponent(discordUserId, application)).WithAccentColor(ColorHelpers.Info);
                    summaryComponent
                        .WithAccentColor(ColorHelpers.Info)
                        .WithComponents([
                            ..summaryComponent.Components.ToList(),
                            new ActionRowProperties([
                                new ButtonProperties($"buildapp_approve_button:{appId}:{discordUserId}", "Approve", ButtonStyle.Success),
                                new ButtonProperties($"buildapp_reject_button:{appId}:{discordUserId}", "Reject", ButtonStyle.Danger),
                                new ButtonProperties($"buildapp_refresh_button:{appId}:{discordUserId}:{(int)RefreshButtonMode.ResolveSummary}", "Refresh", ButtonStyle.Secondary)
                            ])
                        ]);
                }
                
            }
            
            return InteractionCallback.ModifyMessage(options => options.WithComponents([summaryComponent]).WithFlags(MessageFlags.Ephemeral | MessageFlags.IsComponentsV2));
        }

        public enum RefreshButtonMode
        {
            FullSummary,
            ResolveSummary
        }
        
    }
    
    public class ReviewModalInteractions(IApplicationService applicationService, IGreenfieldApiService gfApiService, RestClient restClient) : ComponentInteractionModule<ModalInteractionContext>
    {
        
        [ComponentInteraction("buildapp_approve_modal")]
        public async Task ApproveApplicationModal(long appId, ulong discordUserId)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredModifyMessage);
            
            var actualApplicationResult = await gfApiService.GetApplicationById(appId);
            if (!actualApplicationResult.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackApplicationNotFound(appId)]));
                return;
            }

            var comments = Context.Components.FromLabel<TextInput>()?.Value;
            
            var approveApplicationResult = await applicationService.AcceptApplication(discordUserId, application, comments);
            if (!approveApplicationResult.TryGetDataNonNull(out var acceptanceChannel))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackFailedToApproveApplication(appId, approveApplicationResult.ErrorMessage)]));
                return;
            }
            
            var summaryComponent = await applicationService.GenerateApplicationSummaryComponent(discordUserId, application, true);
            
            var messageId = Context.Interaction.Message!.Id;
            var channelId = Context.Channel.Id;
            _ = restClient.ModifyMessageAsync(channelId, messageId, options =>
            {
                var componentList = summaryComponent.Components.ToList();
                componentList.Add(new ActionRowProperties([
                    new ButtonProperties("button_does_nothing", "Approved!", EmojiProperties.Standard("✔️"), ButtonStyle.Success).WithDisabled(),
                    new LinkButtonProperties($"https://discord.com/channels/{Context.Guild?.Id}/{acceptanceChannel}", "Go to Application")
                ]));
                summaryComponent
                    .WithComponents(componentList)
                    .WithAccentColor(ColorHelpers.Success);
                options.WithComponents([summaryComponent]);
            });
        }

        [ComponentInteraction("buildapp_reject_modal")]
        public async Task RejectApplicationModal(long appId, ulong discordUserId)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredModifyMessage);

            var actualApplicationResult = await gfApiService.GetApplicationById(appId);
            if (!actualApplicationResult.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackApplicationNotFound(appId)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var customRejectionReason = Context.Components.FromLabel<TextInput>()?.Value;
            var definedRejectionReasons = Context.Components.FromLabel<CheckboxGroup>()?.CheckedValues;

            var commentResult = GetRejectionComment(customRejectionReason, definedRejectionReasons?.ToList());
            if (!commentResult.TryGetDataNonNull(out var comment))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackBadRejectionComment(appId)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var denyApplicationResult = await applicationService.DenyApplication(discordUserId, application, comment);
            if (!denyApplicationResult.TryGetDataNonNull(out var denialChannel))
            {
                _ = Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties().WithEmbeds([CallbackFailedToRejectApplication(appId, denyApplicationResult.ErrorMessage)]).WithFlags(MessageFlags.Ephemeral));
                return;
            }

            var summaryComponent = await applicationService.GenerateApplicationSummaryComponent(discordUserId, application, true);

            var messageId = Context.Interaction.Message!.Id;
            var channelId = Context.Channel.Id;
            _ = restClient.ModifyMessageAsync(channelId, messageId, options =>
            {
                var componentList = summaryComponent.Components.ToList();
                componentList.Add(new ComponentSeparatorProperties());
                componentList.Add(new ActionRowProperties([
                    new ButtonProperties("button_does_nothing", "Rejected!", EmojiProperties.Standard("✖️"), ButtonStyle.Danger).WithDisabled(),
                    new LinkButtonProperties($"https://discord.com/channels/{Context.Guild?.Id}/{denialChannel}", "Go to Application")
                ]));
                summaryComponent
                    .WithComponents(componentList)
                    .WithAccentColor(ColorHelpers.Failure);
                options.WithComponents([summaryComponent]);
            });
        }

        private static Result<string> GetRejectionComment(string? customReason, List<string>? definedReasons)
        {
            if (string.IsNullOrWhiteSpace(customReason) && (definedReasons is null || definedReasons.Count == 0))
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