using System.Net.Mime;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;

namespace GreenbotTwo.Interactions.BuildApplications;

public class ApplyInteractions
{

    public const string ApplicationUserSelectionButton = "select_user_for_application";
    public const string ApplicationLinkNewAccountButton = "link_new_account_for_app";

    #region  Normal Application Embeds

    public static readonly EmbedProperties ApplicationStartEmbed = GenericEmbeds.Info("Greenfield Application Service",
        "Hi! Welcome to Greenfield, and thank you for considering becoming a build member! Please complete all sections of the application by clicking the buttons below and filling out each required form.\n\nIf you have any questions or concerns about the application process, please ask a Staff member for assistance. Your progress before final submission (except for image uploads) will be saved.\n\nGood Luck!");
    private static readonly EmbedProperties ApplicationSubmitEmbed = GenericEmbeds.Success("Greenfield Application Service",
        "Thank you for submitting your application to join the Greenfield Build Team! We appreciate your interest and the time you've taken to apply. Your application is processing; we will be in touch with you regarding the status of your application as soon as possible. To view the status of your application, you may use the command `/appstatus`. Good luck!");
    private static readonly EmbedProperties ApplicationPreparingEmbed = GenericEmbeds.Info("Greenfield Application Service",
        "Preparing your application...");
    
    #endregion

    #region User Error Embeds

    private static readonly EmbedProperties UserErrorNoApplicationInProgress = GenericEmbeds.UserError("Greenfield Application Service",
        "It appears you do not have an application in progress. To start a new application, use `/apply`.");
    private static readonly EmbedProperties UserErrorTermsOfServiceDisagreement = GenericEmbeds.UserError("Greenfield Application Service",
        "Sorry, but you must agree with our Terms and Conditions to be considered a build team member. Your application has been declined - you may start a new application at any time by using `/apply`!");
    private static readonly EmbedProperties UserErrorUnknownUser = GenericEmbeds.UserError("Greenfield Application Service",
        "We were unable to find a user associated with the selected account. Please try again with a different user.");
    private static readonly EmbedProperties UserErrorApplicationAlreadyInProgress = GenericEmbeds.UserError("Greenfield Application Service",
        "You already have an application in progress. Please complete your existing application before starting a new one.");
    private static readonly EmbedProperties UserErrorApplicationAlreadyUnderReview = GenericEmbeds.UserError("Greenfield Application Service",
        "Your application is already under review. You cannot start a new one until the current application has been processed.");
    private static readonly EmbedProperties UserErrorNoDiscordAccountsLinked = GenericEmbeds.UserError("Greenfield Application Service",
        "It appears you do not have any Discord accounts linked to the selected Minecraft account. Please link a Discord account and try again.");
    private static readonly EmbedProperties UserErrorCurrentDiscordAccountNotLinkedToSelectedUser = GenericEmbeds.UserError("Greenfield Application Service",
        "The Discord account you are using to apply is not linked to the selected Minecraft account. Please link this Discord account to the Minecraft account and try again.");
    
    #endregion

    #region Validation Errors

    private static readonly EmbedProperties ValidationInvalidAge = GenericEmbeds.UserError("Validation Error",
        "The age you provided is not a valid number. Please ensure you have entered it correctly and try again.");
    private static readonly Func<string, EmbedProperties> ValidationInvalidFileAttachment = fileName => GenericEmbeds.UserError("Validation Error",
        $"The attachment `{fileName}` is not a valid image file. Please provide only image files to showcase your building experience.");
    private static readonly Func<string, string, EmbedProperties> ValidationAttachmentTooLarge = (fileName, maxSize) => GenericEmbeds.UserError("Validation Error",
        $"The attachment `{fileName}` exceeds the maximum allowed size of {maxSize}. Please provide smaller image files to showcase your building experience.");
    
    #endregion

    #region Internal Error Embeds

    private static readonly EmbedProperties InternalErrorApplicationSubmitCalledWhenComplete = GenericEmbeds.InternalError("Internal Application Error", 
        "Your application is not yet complete. Tossing the current application (you shouldn't have been able to reach this step) - please start a new application using `/apply` and ensure all sections are completed before submitting.");
    private static readonly Func<Result<long>, EmbedProperties> InternalErrorSubmissionFailure = (submitResult) => GenericEmbeds.InternalError("Internal Application Error",
            $"There was an error while trying to submit your application: {submitResult.ErrorMessage ?? $"Submit was {(submitResult.IsSuccessful ? "" : "not")} successful. Result was {submitResult.GetNonNullOrThrow()}"}. Report this error to NJDaeger.")
        .WithFooter(new EmbedFooterProperties().WithText("Sorry about this inconvenience!"));
    private static readonly Func<Result<Application>, EmbedProperties> InternalErrorApplicationRetrievalFailure = (applicationResponse) => GenericEmbeds.InternalError("Internal Application Error",
        applicationResponse.ErrorMessage ?? "An unknown error occurred while trying to retrieve your application.");
    private static readonly EmbedProperties InternalErrorFailedToGetDiscordConnectionUrl = GenericEmbeds.InternalError("Internal Application Error",
        "An internal error occurred while trying to generate a Discord connection URL. Try to use the `/accounts` command to link your Discord account to your Minecraft account, then try applying again.");
    
    #endregion
    
    /// <summary>
    /// When a user is selecting which account to use for their application.
    /// </summary>
    /// <param name="applicationService"></param>
    /// <param name="gfApiService"></param>
    public class ApplyUserSelectionInteractions(IApplicationService applicationService, IGreenfieldApiService gfApiService) : ComponentInteractionModule<StringMenuInteractionContext>
    {
        
        [ComponentInteraction(ApplicationUserSelectionButton)]
        public async Task UserSelectionButton()
        {
            await Context.Interaction.SendResponse([ApplicationPreparingEmbed], MessageFlags.Ephemeral);
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (isInProgress)
            {
                await Context.Interaction.ModifyResponse([UserErrorApplicationAlreadyInProgress]);
                return;
            }
            
            var isUnderReviewResult = await applicationService.HasApplicationUnderReview(Context.User.Id);
            if (!isUnderReviewResult.IsSuccessful || isUnderReviewResult.GetNonNullOrThrow())
            {
                await Context.Interaction.ModifyResponse([UserErrorApplicationAlreadyUnderReview]);
                return;
            }

            var selectedId = Context.SelectedValues[0];
            if (string.IsNullOrWhiteSpace(selectedId) || !long.TryParse(selectedId, out var userId) || !(await gfApiService.GetUserById(userId)).TryGetDataNonNull(out var user))
            {
                await Context.Interaction.ModifyResponse([UserErrorUnknownUser]);
                return;
            }

            var sendLinkAccountButton = false;
            var linkedDiscordAccountsResult = await gfApiService.GetDiscordAccountsForUser(user.UserId);
            if (!linkedDiscordAccountsResult.TryGetDataNonNull(out var linkedDiscordAccounts) || linkedDiscordAccounts.Count == 0)
            {
                sendLinkAccountButton = true;
                await Context.Interaction.ModifyResponse([UserErrorNoDiscordAccountsLinked]);
            }
            else if (linkedDiscordAccounts.All(a => a.DiscordSnowflake != Context.User.Id))
            {
                sendLinkAccountButton = true;
                await Context.Interaction.ModifyResponse([UserErrorCurrentDiscordAccountNotLinkedToSelectedUser]);
            }

            if (sendLinkAccountButton)
            {
                var channelUrl = $"discord://discord.com/channels/{Context.Guild?.Id}/{Context.Channel.Id}";
                var discordConnectionButtonResult = await applicationService.GenerateDiscordLinkComponent(user.UserId, channelUrl);
                if (!discordConnectionButtonResult.TryGetDataNonNull(out var discordConnectComponent))
                {
                    await Context.Interaction.SendFollowupResponse([InternalErrorFailedToGetDiscordConnectionUrl], [], MessageFlags.Ephemeral);
                    return;
                }
                await Context.Interaction.SendFollowupResponse([], [discordConnectComponent], MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
                return;
            }
            
            var application = applicationService.StartApplication(Context.User.Id, user);
            await Context.Interaction.ModifyResponse([ApplicationStartEmbed], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
        }
    }
    
    /// <summary>
    /// These are all the buttons that make up the application process.
    /// </summary>
    /// <param name="applicationService"></param>
    public class ApplyMessageButtonInteractions(IOptions<BuilderApplicationSettings> buildAppSettings, IApplicationService applicationService, IGreenfieldApiService apiService, IAccountLinkService accountLinkService) : ComponentInteractionModule<ButtonInteractionContext>
    {

        [ComponentInteraction("start_application")]
        public async Task StartApplicationButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (isInProgress)
            {
                await Context.Interaction.SendResponse([UserErrorApplicationAlreadyInProgress], MessageFlags.Ephemeral);
                return;
            }
            
            var userResponse = await apiService.GetUsersConnectedToDiscordAccount(Context.User.Id);
            if (!userResponse.TryGetData(out var connectionWithUsers) && userResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                await Context.Interaction.SendResponse([UserErrorUnknownUser], MessageFlags.Ephemeral);
                return;
            }
            
            var isUnderReviewResult = await applicationService.HasApplicationUnderReview(Context.User.Id);
            if (!isUnderReviewResult.IsSuccessful || isUnderReviewResult.GetNonNullOrThrow())
            {
                await Context.Interaction.SendResponse([UserErrorApplicationAlreadyUnderReview], MessageFlags.Ephemeral);
                return;
            }
            
            var users = connectionWithUsers?.Users ?? [];
            if (users.Count == 0)          {
                var cached = accountLinkService.GetCachedVerifiedUser(Context.User.Id);
                if (cached is not null)
                    users.Add(cached);
            }
            var selectionComponent = await accountLinkService.GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor.Application, users);
            await Context.Interaction.SendResponse(components: [selectionComponent], flags: MessageFlags.Ephemeral | MessageFlags.IsComponentsV2);
        }
        
        [ComponentInteraction(ApplicationLinkNewAccountButton)]
        public async Task<InteractionCallbackProperties> LinkNewAccount()
        {
            accountLinkService.ClearCachedVerifiedUser(Context.User.Id);
            accountLinkService.ClearInProgressAccountLink(Context.User.Id);
            accountLinkService.GetOrStartAccountLinkForm(Context.User.Id, AccountLinkService.UserSelectionFor.Application);
            var modal = new ModalProperties("authhub_username_modal", "Minecraft Account Link")
                .WithComponents([
                    new TextDisplayProperties(
                        "We require you have direct access to a Java Edition Minecraft account. We will need to verify your account exists. What is your Minecraft username?"),
                    new LabelProperties("Minecraft Username",
                        new TextInputProperties("mc_username", TextInputStyle.Short).WithRequired())
                ]);

            return InteractionCallback.Modal(modal);
        }
        
        [ComponentInteraction("apply_terms")]
        public InteractionCallbackProperties TermsAndConditionsButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([UserErrorNoApplicationInProgress]));
            
            var modal = new ModalProperties("apply_terms_modal", "Section 1 - Terms and Conditions")
                .WithComponents([
                    new TextDisplayProperties("Before you continue with your application, the terms and conditions for build members (under the *\"Build Member Conditions\"* heading) must be understood and agreed to. You can find and read these conditions at the link below!"),
                    new TextDisplayProperties("~~-->~~ [Build Member Conditions](https://www.greenfieldmc.net/conditions/) ~~<--~~"),
                    new LabelProperties("Do you agree with the Terms and Conditions?", new CheckboxGroupProperties("apply_modal_terms_agreement")
                            .WithOptions([new CheckboxGroupOptionProperties("I agree to the Terms and Conditions.", "agree")])
                            .WithRequired()
                        ).WithDescription("You must agree to the terms and conditions to proceed with your application.")
                ]);
            
            return InteractionCallback.Modal(modal);
        }

        [ComponentInteraction("apply_user_info")]
        public InteractionCallbackProperties UserInformationButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([UserErrorNoApplicationInProgress]));

            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");
            
            var modal = new ModalProperties("apply_user_info_modal", "Section 2 - Personal Information")
                .WithComponents([
                    new LabelProperties("Age", new TextInputProperties("apply_modal_age", TextInputStyle.Short)
                            .WithMaxLength(3)
                            .WithValue(application.Age == -1 ? null : application.Age.ToString()))
                        .WithDescription("By providing this, you are complying with Discord's Terms of Service."),
                    new LabelProperties("Nationality", new TextInputProperties("apply_modal_nationality", TextInputStyle.Short).WithRequired(false).WithMaxLength(56).WithValue(application.Nationality))
                        .WithDescription("What country are you from? (Only if you'd like to share with us)")
                ]);
            
            return InteractionCallback.Modal(modal);
        }
        
        [ComponentInteraction("apply_building_experience")]
        public InteractionCallbackProperties BuildingExperienceButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([UserErrorNoApplicationInProgress]));

            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");
            
            var modal = new ModalProperties("apply_building_experience_modal", "Section 3 - Building Experience")
                .WithComponents([
                    new TextDisplayProperties($"Housing is a core aspect of any city. It is also one of the easiest ways for us to accurately gauge your building skills. Please provide some examples of houses __**you**__ have built **__in Minecraft__** that would fit into a typical North American city. \n\n*Note: There is a {buildAppSettings.Value.GetMaxFileSizeNice()} limit per file.*"),
                    new LabelProperties("North American House Build(s)", new FileUploadProperties("apply_modal_house_builds")
                        .WithMinValues(buildAppSettings.Value.MinimumNumberOfHouseImages == 0 ? null : buildAppSettings.Value.MinimumNumberOfHouseImages)
                        .WithMaxValues(buildAppSettings.Value.MaximumNumberOfHouseImages)),
                    new TextDisplayProperties($"Optionally, you may also provide any other builds you have created in Minecraft that you feel best represent your ability to contribute to a large-scale city project like Greenfield. \n\n*Note: There is a {buildAppSettings.Value.GetMaxFileSizeNice()} limit per file.*"),
                    new LabelProperties("Other Build(s)", new FileUploadProperties("apply_modal_other_builds")
                        .WithRequired(false)
                        .WithMinValues(buildAppSettings.Value.MinimumNumberOfOtherImages == 0 ? null : buildAppSettings.Value.MinimumNumberOfOtherImages)
                        .WithMaxValues(buildAppSettings.Value.MaximumNumberOfOtherImages)),
                    new LabelProperties("Additional Information About Your Builds", new TextInputProperties("apply_modal_build_info", TextInputStyle.Paragraph)
                            .WithRequired(false)
                            .WithMaxLength(buildAppSettings.Value.MaximumAdditionalBuildingInfoLength)
                            .WithValue(application.AdditionalBuildingInformation))
                        .WithDescription("Feel free to provide any additional context or information about your provided builds here.")
                ]);
            
            return InteractionCallback.Modal(modal);
        }
        
        [ComponentInteraction("apply_closing_thoughts")]
        public async Task<InteractionCallbackProperties> ClosingThoughtsButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([UserErrorNoApplicationInProgress]));

            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");
            var modal = new ModalProperties("apply_closing_thoughts_modal", "Section 4 - Closing Remarks")
                .WithComponents([
                    new LabelProperties("Why do you want join our Build Team?", new TextInputProperties("apply_modal_why_join", TextInputStyle.Paragraph)
                            .WithRequired()
                            .WithMaxLength(buildAppSettings.Value.MaximumWhyJoinLength)
                            .WithValue(application.WhyJoinGreenfield == string.Empty ? null : application.WhyJoinGreenfield))
                        .WithDescription("We're glad you are taking interest in our team, we're curious, why do you want to be a part of it?"),
                    new LabelProperties("Additional Comments or Questions?", new TextInputProperties("apply_modal_additional_comments", TextInputStyle.Paragraph)
                            .WithRequired(false)
                            .WithMaxLength(buildAppSettings.Value.MaximumAdditionalCommentsLength)
                            .WithValue(application.AdditionalComments))
                        .WithDescription("Feel free to share any additional comments, questions, or information you'd like us to know.")
                ]);
            
            return InteractionCallback.Modal(modal);
        }
        
        [ComponentInteraction("apply_final_submit")]
        public InteractionCallbackProperties SubmitApplicationButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([UserErrorNoApplicationInProgress]));

            var modal = new ModalProperties("apply_final_submit_modal", "Submission")
                .WithComponents([
                    new TextDisplayProperties("By submitting your application, you confirm that all information provided is accurate and truthful to the best of your knowledge. You understand that any false information may lead to disqualification from the application process. Once you submit, you will not be able to modify your application responses from this attempt."),
                    new TextDisplayProperties("If you are happy with your application, please press the submit button below to send it in for review!")
                ]);
            
            return InteractionCallback.Modal(modal);
        }
        
    }

    /// <summary>
    /// These are all the modals that make up the application process.
    /// </summary>
    /// <param name="applicationService"></param>
    /// <param name="mojangService"></param>
    /// <param name="restClient"></param>
    public class ApplyModalInteractions(IOptions<BuilderApplicationSettings> buildAppSettings, IApplicationService applicationService, IGreenfieldApiService gfApiService) : ComponentInteractionModule<ModalInteractionContext>
    {
        
        /// <summary>
        /// Handle the submission of the Terms and Conditions modal.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        [ComponentInteraction("apply_terms_modal")]
        public async Task HandleTermsSubmission()
        {
            var currentComponents = Context.Interaction.Message?.Components ?? [];
            var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
            var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
            await Context.Interaction.SendModifyResponse(components: [new ActionRowProperties().WithComponents(newButtons)]);
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                await Context.Interaction.ModifyResponse([UserErrorNoApplicationInProgress], []);
                return;
            }

            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");

            var selection = (Context.Components.FromLabel<CheckboxGroup>()?.CheckedValues ?? [])[0];
            if (!selection.Equals("agree", StringComparison.InvariantCultureIgnoreCase)) 
            {
                applicationService.ClearInProgressApplication(Context.User.Id);
                await Context.Interaction.ModifyResponse([UserErrorTermsOfServiceDisagreement], []);
                return;
            }

            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.TermsOfService] = selection.Equals("agree", StringComparison.InvariantCultureIgnoreCase);
            await Context.Interaction.ModifyResponse(components: [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
        }

        /// <summary>
        /// Handle the submission of the User Information modal.
        /// </summary>
        [ComponentInteraction("apply_user_info_modal")]
        public async Task HandleUserInfoSubmission()
        {
            var currentComponents = Context.Interaction.Message?.Components ?? [];
            var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
            var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
            await Context.Interaction.SendModifyResponse(components: [new ActionRowProperties().WithComponents(newButtons)]);
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                await Context.Interaction.ModifyResponse([UserErrorNoApplicationInProgress], []);
                return;
            }
            
            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");
            
            var validationMessages = new List<EmbedProperties>();
            if (!int.TryParse(Context.Components.FromLabel<TextInput>("apply_modal_age")!.Value, out var ageNumber) || ageNumber <= 0)
            {
                application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.PersonalInformation] = false;
                validationMessages.Add(ValidationInvalidAge);
            }

            application.Age = ageNumber;
            application.Nationality = Context.Components.FromLabel<TextInput>("apply_modal_nationality")!.Value;
            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.PersonalInformation] = validationMessages.Count == 0;

            await Context.Interaction.ModifyResponse([ApplicationStartEmbed, ..validationMessages.Take(4)], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
        }

        /// <summary>
        /// Handle the submission of the Building Experience modal.
        /// </summary>
        [ComponentInteraction("apply_building_experience_modal")]
        public async Task HandleBuildingExperienceSubmission()
        {
            var currentComponents = Context.Interaction.Message?.Components ?? [];
            var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
            var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
            await Context.Interaction.SendModifyResponse(components: [new ActionRowProperties().WithComponents(newButtons)]);
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                await Context.Interaction.ModifyResponse([UserErrorNoApplicationInProgress], []);
                return;
            }

            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");
            
            var validationMessages = new List<EmbedProperties>();
            var houseBuilds = Context.Components.FromLabel<FileUpload>("apply_modal_house_builds")!.Attachments.ToList();
            foreach (var attachment in houseBuilds)
            {
                if (attachment.ContentType is null || !new ContentType(attachment.ContentType).MediaType.Contains("image/"))
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(ValidationInvalidFileAttachment(attachment.FileName));
                }
                if (attachment.Size > buildAppSettings.Value.MaximumPerFileSizeBytes)
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(ValidationAttachmentTooLarge(attachment.FileName, buildAppSettings.Value.GetMaxFileSizeNice()));
                }
            }
            
            var otherBuilds = Context.Components.FromLabel<FileUpload>("apply_modal_other_builds")!.Attachments.ToList();
            foreach (var attachment in otherBuilds)
            {
                if (attachment.ContentType is null || !new ContentType(attachment.ContentType).MediaType.Contains("image/"))
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(ValidationInvalidFileAttachment(attachment.FileName));
                }
                
                if (attachment.Size > buildAppSettings.Value.MaximumPerFileSizeBytes)
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(ValidationAttachmentTooLarge(attachment.FileName, buildAppSettings.Value.GetMaxFileSizeNice()));
                }
            }
            
            application.Images = houseBuilds
                .Select(a => new BuilderApplicationImageUpload(a.Url, "TempHouse"))
                .Concat(otherBuilds.Select(a => new BuilderApplicationImageUpload(a.Url, "TempOther")))
                .ToList();
            application.AdditionalBuildingInformation = Context.Components.FromLabel<TextInput>("apply_modal_build_info")!.Value;
            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = validationMessages.Count == 0;
            
            await Context.Interaction.ModifyResponse([ApplicationStartEmbed, ..validationMessages.Take(4)], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
        }

        /// <summary>
        /// Handle the submission of the Closing Thoughts modal.
        /// </summary>
        [ComponentInteraction("apply_closing_thoughts_modal")]
        public async Task HandleClosingThoughtsSubmission()
        {
            var currentComponents = Context.Interaction.Message?.Components ?? [];
            var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
            var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
            await Context.Interaction.SendModifyResponse(components: [new ActionRowProperties().WithComponents(newButtons)]);
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                await Context.Interaction.ModifyResponse([UserErrorNoApplicationInProgress], []);
                return;
            }

            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");
            
            var whyJoin = Context.Components.FromLabel<TextInput>("apply_modal_why_join")?.Value ?? "";

            application.WhyJoinGreenfield = whyJoin;
            application.AdditionalComments = Context.Components.FromLabel<TextInput>("apply_modal_additional_comments")?.Value;
            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.ClosingRemarks] = true;
            
            await Context.Interaction.ModifyResponse([ApplicationStartEmbed], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
        }

        /// <summary>
        /// Handle the final submission of the application.
        /// </summary>
        [ComponentInteraction("apply_final_submit_modal")]
        public async Task HandleFinalSubmission()
        {
            var currentComponents = Context.Interaction.Message?.Components ?? [];
            var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
            var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
            await Context.Interaction.SendModifyResponse(components: [new ActionRowProperties().WithComponents(newButtons)]);
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                await Context.Interaction.ModifyResponse(components: [], embeds: [UserErrorNoApplicationInProgress]);
                return;
            }
           
            
            var application = applicationService.GetApplication(Context.User.Id) ?? throw new InvalidOperationException("Application in progress but could not be retrieved.");

            if (!application.IsComplete()) 
            {
                applicationService.ClearInProgressApplication(Context.User.Id);
                await Context.Interaction.ModifyResponse(components: [], embeds: [InternalErrorApplicationSubmitCalledWhenComplete]);
                return;
            }

            var submitResult = await applicationService.SubmitApplication(Context.User.Id);
            application.Submitted = true;
            
            if (!submitResult.TryGetDataNonNull(out var appId))
            {
                application.Submitted = false;
                await Context.Interaction.ModifyResponse([ApplicationStartEmbed, InternalErrorSubmissionFailure(submitResult)], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
                return;
            }
            
            var submittedAppResponse = await gfApiService.GetApplicationById(appId);
            if (!submittedAppResponse.TryGetDataNonNull(out var submittedApplication))
            {
                application.Submitted = false;
                await Context.Interaction.ModifyResponse([ApplicationStartEmbed, InternalErrorApplicationRetrievalFailure(submittedAppResponse)], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
                return;
            }
            
            _ = applicationService.CompleteAndForwardApplicationToReview(Context.User.Id, submittedApplication);
            
            await Context.Interaction.ModifyResponse([ApplicationSubmitEmbed], [new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())]);
            
        }
    }
   
}