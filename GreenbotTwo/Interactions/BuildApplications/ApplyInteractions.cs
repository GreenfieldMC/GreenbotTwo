using System.Net;
using System.Net.Mime;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
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
    
    public static readonly EmbedProperties ApplicationStartEmbed = GenericEmbeds.Info("Greenfield Application Service",
        "Hi! Welcome to Greenfield, and thank you for considering becoming a build member! Please complete all sections of the application by clicking the buttons below and filling out each required form.\n\nIf you have any questions or concerns about the application process, please ask a Staff member for assistance. Your progress before final submission (except for image uploads) will be saved.\n\nGood Luck!");
    public static readonly EmbedProperties ApplicationSubmitEmbed = GenericEmbeds.Success("Greenfield Application Service",
        "Thank you for submitting your application to join the Greenfield Build Team! Your application has been received and is now under review. We appreciate your interest and the time you've taken to apply. We will be in touch with you regarding the status of your application as soon as possible. Good luck!");
    public static readonly EmbedProperties ApplicationNotInProgressEmbed = GenericEmbeds.UserError("Greenfield Application Service",
        "It appears you do not have an application in progress. Please start a new application to proceed by using `/apply`.");
    public static readonly EmbedProperties TermsOfServiceDisagreedEmbed = GenericEmbeds.UserError("Greenfield Application Service",
        "Sorry, but you must agree with our Terms and Conditions to be considered a build team member. Your application has been declined - you may start a new application at any time by using `/apply`!");
    public static readonly EmbedProperties InvalidMinecraftUsernameEmbed = GenericEmbeds.UserError("Validation Error",
        "The Minecraft username you provided does not appear to be valid. Please ensure it is a valid **Minecraft Java Edition** account and that you have entered it correctly.");
    public static readonly Func<string?, EmbedProperties> MinecraftUsernameValidationFailureEmbed = error => GenericEmbeds.InternalError("Internal Application Error",
        $"There was an error while trying to validate your Minecraft username. It is possibly an issue with the Mojang API. Please try again later. Current error: {error ?? "Unknown."}");
    public static readonly EmbedProperties InvalidAgeEmbed = GenericEmbeds.UserError("Validation Error",
        "The age you provided is not a valid number. Please ensure you have entered it correctly and try again.");
    public static readonly Func<string, EmbedProperties> InvalidFileAttachmentEmbed = fileName => GenericEmbeds.UserError("Validation Error",
        $"The attachment `{fileName}` is not a valid image file. Please provide only image files to showcase your building experience.");
    public static readonly Func<string, EmbedProperties> FileTooLargeAttachmentEmbed = fileName => GenericEmbeds.UserError("Validation Error",
        $"The attachment `{fileName}` exceeds the maximum allowed size of 10MB. Please provide smaller image files to showcase your building experience.");
    public static readonly EmbedProperties InvalidWhyJoinEmbed = GenericEmbeds.UserError("Validation Error",
        "You must provide a reason for why you want to join the Greenfield Build Team to proceed with your application. Please try again.");
    public static readonly EmbedProperties ApplicationSubmitWhenIncompleteFailureEmbed = GenericEmbeds.InternalError("Internal Application Error", 
        "Your application is not yet complete. Tossing the current application (you shouldn't have been able to reach this step) - please start a new application using `/apply` and ensure all sections are completed before submitting.");
    public static readonly Func<Result<long>, EmbedProperties> ErrorSubmittingApplicationEmbed = (submitResult) => GenericEmbeds.InternalError("Internal Application Error",
        $"There was an error while trying to submit your application: {submitResult.ErrorMessage ?? $"Submit was {(submitResult.IsSuccessful ? "" : "not")} successful. Result was {submitResult.GetNonNullOrThrow()}"}. Report this error to NJDaeger.")
        .WithFooter(new EmbedFooterProperties().WithText("Sorry about this inconvenience!"));
    public static readonly Func<Result<bool>, EmbedProperties> ErrorForwardingApplicationEmbed = (forwardResult) => GenericEmbeds.InternalError("Internal Application Error",
        $"There was an error while trying to forward your application for review: {forwardResult.ErrorMessage ?? $"Forward was {(forwardResult.IsSuccessful ? "" : "not")} successful. Result was {forwardResult.GetNonNullOrThrow()}"}. Report this error to NJDaeger.")
        .WithFooter(new EmbedFooterProperties().WithText("Sorry about this inconvenience!"));
    public static readonly EmbedProperties InternalErrorEmbed = GenericEmbeds.InternalError("Internal Application Error",
        "An internal error occurred while trying to retrieve your application. Please try again later.");
    public static readonly Func<Result<BuilderApplicationForm>, EmbedProperties> ApplicationRetrievalErrorEmbed = (applicationResponse) => GenericEmbeds.UserError("Greenfield Application Service",
            applicationResponse.ErrorMessage ?? "An unknown error occurred while trying to retrieve your application.");
    public static readonly Func<Guid, EmbedProperties> UserWithThatAccountAlreadyExistsEmbed = (accountUuid) => GenericEmbeds.UserError("Greenfield Application Service",
        $"The Minecraft account associated with the username you provided is already linked to another Discord account. You may be on the wrong Discord account, or the Minecraft account may be shared with another user who uses a different Discord account. If you believe this is an error, please contact an Administrator to clear the Discord accounts linked with UUID: `{accountUuid}`.");
    public static readonly EmbedProperties FailedToDetermineLinkedAccountsEmbed = GenericEmbeds.InternalError("Internal Application Error",
        "An internal error occurred while trying to determine if your Minecraft account is already linked to another Discord account. Please try again later.");
    public static readonly EmbedProperties UserNotFound = GenericEmbeds.UserError("Greenfield Application Service",
        "We were unable to find a user associated with the selected account. Please try again with a different user.");
    public static readonly EmbedProperties ApplicationCurrentlyInProgress = GenericEmbeds.UserError("Greenfield Application Service",
        "You already have an application in progress. Please complete your existing application before starting a new one.");
    public static readonly EmbedProperties ApplicationUnderReview = GenericEmbeds.UserError("Greenfield Application Service",
        "Your application is already under review. You cannot start a new one until the current application has been processed.");
    
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
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties().WithEmbeds([GenericEmbeds.Info("Greenfield Application Service", "Preparing your application...")]).WithFlags(MessageFlags.Ephemeral)));
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (isInProgress)
            {
                await Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationCurrentlyInProgress]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            var isUnderReviewResult = await applicationService.HasApplicationUnderReview(Context.User.Id);
            if (!isUnderReviewResult.IsSuccessful || isUnderReviewResult.GetNonNullOrThrow())
            {
                await Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationUnderReview]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            var selectedId = Context.SelectedValues.FirstOrDefault();

            if (selectedId is null || string.IsNullOrWhiteSpace(selectedId) || !long.TryParse(selectedId, out var userId) || !(await gfApiService.GetUserById(userId)).TryGetDataNonNull(out var user))
            {
                await Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([UserNotFound]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            var applicationResult = await applicationService.GetOrStartApplication(Context.User.Id, user);
            if (!applicationResult.TryGetDataNonNull(out var application))
            {
                await Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([UserNotFound]).WithFlags(MessageFlags.Ephemeral));
                return;
            }
            
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([ApplicationStartEmbed])
                .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
                .WithFlags(MessageFlags.Ephemeral)
            );
        }
    }
    
    /// <summary>
    /// These are all the buttons that make up the application process.
    /// </summary>
    /// <param name="applicationService"></param>
    public class ApplyMessageButtonInteractions(IOptions<BuilderApplicationSettings> buildAppSettings, IApplicationService applicationService, IGreenfieldApiService apiService, IAccountLinkService accountLinkService) : ComponentInteractionModule<ButtonInteractionContext>
    {
        
        [ComponentInteraction("apply_terms")]
        public InteractionCallbackProperties TermsAndConditionsButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));
            
            var modal = new ModalProperties("apply_terms_modal", "Section 1 - Terms and Conditions")
                .WithComponents([
                    new TextDisplayProperties("Before you continue with your application, the terms and conditions for build members (under the *\"Build Member Conditions\"* heading) must be understood and agreed to. You can find and read these conditions at the link below!"),
                    new TextDisplayProperties("~~-->~~ [Build Member Conditions](https://www.greenfieldmc.net/conditions/) ~~<--~~"),
                    new LabelProperties("Do you agree with the Terms and Conditions?", new StringMenuProperties("apply_modal_terms_agreement", 
                            [
                                    new StringMenuSelectOptionProperties("I agree with the Terms and Conditions.", "agree"), 
                                    new StringMenuSelectOptionProperties("I do NOT agree with the Terms and Conditions.", "disagree")
                                ])
                                .WithMinValues(1)
                                .WithMaxValues(1))
                        .WithDescription("You must agree to the terms and conditions to proceed with your application.")
                ]);
            
            return InteractionCallback.Modal(modal);
        }

        [ComponentInteraction("apply_user_info")]
        public async Task<InteractionCallbackProperties> UserInformationButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));

            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.IsSuccessful) return InteractionCallback.ModifyMessage(options => options.WithComponents([])
                .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)]));
            var application = applicationResponse.GetNonNullOrThrow();
            
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
        public async Task<InteractionCallbackProperties> BuildingExperienceButton()
        {
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));

            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.IsSuccessful) return InteractionCallback.ModifyMessage(options => options.WithComponents([])
                .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)]));
            var application = applicationResponse.GetNonNullOrThrow();
            
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
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));

            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.IsSuccessful) return InteractionCallback.ModifyMessage(options => options.WithComponents([])
                .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)]));
            var application = applicationResponse.GetNonNullOrThrow();
            
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
            if (!isInProgress) return InteractionCallback.ModifyMessage(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));

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
    public class ApplyModalInteractions(IOptions<BuilderApplicationSettings> buildAppSettings, IApplicationService applicationService, IMojangService mojangService, RestClient restClient, IGreenfieldApiService gfApiService) : ComponentInteractionModule<ModalInteractionContext>
    {
        
        [ComponentInteraction("apply_terms_modal")]
        public async Task HandleTermsSubmission()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options =>
            {
                var currentComponents = Context.Interaction.Message?.Components ?? [];
                var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
                var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
                options.WithComponents([new ActionRowProperties().WithComponents(newButtons)]);
            }));
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));
                return;
            }

            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithComponents([])
                    .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)])
                );
                return;
            }

            var selection = (Context.Components.FromLabel<StringMenu>()?.SelectedValues ?? [])[0];
            
            if (selection.Equals("disagree", StringComparison.InvariantCultureIgnoreCase)) 
            {
                applicationService.ClearInProgressApplication(Context.User.Id);
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithComponents([])
                    .WithEmbeds([TermsOfServiceDisagreedEmbed])
                );
                return;
            }

            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.TermsOfService] = selection.Equals("agree", StringComparison.InvariantCultureIgnoreCase);
            
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
            );
        }

        [ComponentInteraction("apply_user_info_modal")]
        public async Task HandleUserInfoSubmission()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options =>
            {
                var currentComponents = Context.Interaction.Message?.Components ?? [];
                var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
                var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
                options.WithComponents([new ActionRowProperties().WithComponents(newButtons)]);
            }));
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));
                return;
            }
            
            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.TryGetDataNonNull(out var application))
            {
                await Context.Interaction.ModifyResponseAsync(options => options
                    .WithComponents([])
                    .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)])
                );
                return;
            }
            
            var validationMessages = new List<EmbedProperties>();
            
            if (!int.TryParse(Context.Components.FromLabel<TextInput>("apply_modal_age")!.Value, out var ageNumber) || ageNumber <= 0)
            {
                application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.PersonalInformation] = false;
                validationMessages.Add(InvalidAgeEmbed);
            }

            application.Age = ageNumber;
            application.Nationality = Context.Components.FromLabel<TextInput>("apply_modal_nationality")!.Value;
            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.PersonalInformation] = validationMessages.Count == 0;

            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
                .WithEmbeds([ApplicationStartEmbed, ..validationMessages.Take(4)])
            );
        }

        [ComponentInteraction("apply_building_experience_modal")]
        public async Task HandleBuildingExperienceSubmission()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options =>
            {
                var currentComponents = Context.Interaction.Message?.Components ?? [];
                var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
                var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
                options.WithComponents([new ActionRowProperties().WithComponents(newButtons)]);
            }));
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));
                return;
            }

            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithComponents([])
                    .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)])
                );
                return;
            }
            
            var validationMessages = new List<EmbedProperties>();
            
            var houseBuilds = Context.Components.FromLabel<FileUpload>("apply_modal_house_builds")!.Attachments.ToList();
            foreach (var attachment in houseBuilds)
            {
                if (attachment.ContentType is null || !new ContentType(attachment.ContentType).MediaType.Contains("image/"))
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(InvalidFileAttachmentEmbed(attachment.FileName));
                }
                if (attachment.Size > buildAppSettings.Value.MaximumPerFileSizeBytes)
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(FileTooLargeAttachmentEmbed(attachment.FileName));
                }
            }
            
            var otherBuilds = Context.Components.FromLabel<FileUpload>("apply_modal_other_builds")!.Attachments.ToList();
            foreach (var attachment in otherBuilds)
            {
                if (attachment.ContentType is null || !new ContentType(attachment.ContentType).MediaType.Contains("image/"))
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(InvalidFileAttachmentEmbed(attachment.FileName));
                }
                
                if (attachment.Size > buildAppSettings.Value.MaximumPerFileSizeBytes)
                {
                    application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = false;
                    validationMessages.Add(FileTooLargeAttachmentEmbed(attachment.FileName));
                }
            }
            
            application.Images = houseBuilds
                .Select(a => new BuilderApplicationImageUpload(a.Url, "TempHouse"))
                .Concat(otherBuilds.Select(a => new BuilderApplicationImageUpload(a.Url, "TempOther")))
                .ToList();
            application.AdditionalBuildingInformation = Context.Components.FromLabel<TextInput>("apply_modal_build_info")!.Value;
            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.BuildingExperience] = validationMessages.Count == 0;
            
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
                .WithEmbeds([ApplicationStartEmbed, ..validationMessages.Take(4)])
            );
        }

        [ComponentInteraction("apply_closing_thoughts_modal")]
        public async Task HandleClosingThoughtsSubmission()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options =>
            {
                var currentComponents = Context.Interaction.Message?.Components ?? [];
                var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
                var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
                options.WithComponents([new ActionRowProperties().WithComponents(newButtons)]);
            }));
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));
                return;
            }

            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.TryGetDataNonNull(out var application)) 
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithComponents([])
                    .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)])
                );
                return;
            }
            
            var validationMessages = new List<EmbedProperties>();
            
            var whyJoin = Context.Components.FromLabel<TextInput>("apply_modal_why_join")?.Value ?? "";
            if (string.IsNullOrWhiteSpace(whyJoin))
            {
                application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.ClosingRemarks] = false;
                validationMessages.Add(InvalidWhyJoinEmbed);
            }

            application.WhyJoinGreenfield = whyJoin;
            application.AdditionalComments = Context.Components.FromLabel<TextInput>("apply_modal_additional_comments")?.Value;
            application.SectionsCompleted[BuilderApplicationForm.ApplicationSections.ClosingRemarks] = validationMessages.Count == 0;
            
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
                .WithEmbeds([ApplicationStartEmbed, ..validationMessages.Take(4)])
            );
        }

        [ComponentInteraction("apply_final_submit_modal")]
        public async Task HandleFinalSubmission()
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options =>
            {
                var currentComponents = Context.Interaction.Message?.Components ?? [];
                var existingButtons = currentComponents.OfType<ActionRow>().First().Components.OfType<Button>().ToList();
                var newButtons = existingButtons.Select(button => new ButtonProperties(button.CustomId, button.Label ?? "", button.Style).WithDisabled()).ToList();
                options.WithComponents([new ActionRowProperties().WithComponents(newButtons)]);
            }));
            
            var isInProgress = applicationService.HasApplicationInProgress(Context.User.Id);
            if (!isInProgress)
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([]).WithEmbeds([ApplicationNotInProgressEmbed]));
                return;
            }
           
            
            var applicationResponse = await applicationService.GetOrStartApplication(Context.User.Id);
            if (!applicationResponse.TryGetDataNonNull(out var application))
            {
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([])
                    .WithEmbeds([applicationResponse.StatusCode == HttpStatusCode.InternalServerError ? InternalErrorEmbed : ApplicationRetrievalErrorEmbed(applicationResponse)]));
                return;
            }

            if (!application.IsComplete()) 
            {
                applicationService.ClearInProgressApplication(Context.User.Id);
                _ = Context.Interaction.ModifyResponseAsync(options => options.WithComponents([])
                    .WithEmbeds([ApplicationSubmitWhenIncompleteFailureEmbed]));
                return;
            }

            application.Submitted = true;

            var submitResult = await applicationService.SubmitApplication(Context.User.Id);
            if (!submitResult.TryGetDataNonNull(out var appId))
            {
                application.Submitted = false;
                _ = Context.Interaction.ModifyResponseAsync(options => options
                    .WithComponents([
                        new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())
                    ])
                    .WithEmbeds([ApplicationStartEmbed, ErrorSubmittingApplicationEmbed(submitResult)]));
                return;
            }
            
            var submittedAppResponse = await gfApiService.GetApplicationById(appId);
            if (!submittedAppResponse.TryGetDataNonNull(out var submittedApplication))
            {
                application.Submitted = false;
                    _ = Context.Interaction.ModifyResponseAsync(options => options
                        .WithComponents([
                            new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())
                        ])
                        .WithEmbeds([ApplicationStartEmbed, ErrorSubmittingApplicationEmbed(submitResult)]));
                return;
            }
            
            _ = applicationService.CompleteAndForwardApplicationToReview(Context.User.Id, submittedApplication);
            
            // var forwardResult = await applicationService.ForwardApplicationToReview(application.DiscordId, submittedApplication);
            // if (!forwardResult.TryGetDataNonNull(out _))
            // {
            //     application.Submitted = false;
            //     _ = Context.Interaction.ModifyResponseAsync(options => options
            //         .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
            //         .WithEmbeds([ApplicationStartEmbed, ErrorForwardingApplicationEmbed(forwardResult)]));
            //     return;
            // }
            
            _ = Context.Interaction.ModifyResponseAsync(options =>
            {
                options
                    .WithComponents([new ActionRowProperties().WithComponents(application.GenerateButtonsForApplication())])
                    .WithEmbeds([ApplicationSubmitEmbed]);
            });
        }
    }
   
}