using System.Collections.Concurrent;
using System.Net;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Embeds;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;
using User = GreenbotTwo.Models.GreenfieldApi.User;

namespace GreenbotTwo.Services;

public class ApplicationService(IOptions<BuilderApplicationSettings> options, RestClient restClient, IGreenfieldApiService gfApiService) : IApplicationService
{
    
    private static readonly IDictionary<ulong, BuilderApplicationForm> Applications = new ConcurrentDictionary<ulong, BuilderApplicationForm>();
    
    public bool ClearInProgressApplication(ulong discordId)
    {
        return Applications.Remove(discordId);
    }

    public IEnumerable<BuilderApplicationForm> GetInProgressApplications()
    {
        return Applications.Values;
    }

    public BuilderApplicationForm StartApplication(ulong discordSnowflake, User user)
    {
        var application = new BuilderApplicationForm(user, discordSnowflake);
        Applications[discordSnowflake] = application;
        return application;
    }

    public BuilderApplicationForm? GetApplication(ulong discordId)
    {
        return Applications.TryGetValue(discordId, out var application) 
            ? application 
            : null;

        // if (Applications.TryGetValue(discordId, out var application)) return Result<BuilderApplicationForm>.Success(application);
        //
        // if (user is null) 
        //     return Result<BuilderApplicationForm>.Failure("No in-progress application found, and no user data provided to start a new application.", HttpStatusCode.InternalServerError);
        //
        // var activeAppResult = await HasApplicationUnderReview(discordId);
        // if (!activeAppResult.IsSuccessful) return Result<BuilderApplicationForm>.Failure(activeAppResult.ErrorMessage ?? "Failed to retrieve application status.", activeAppResult.StatusCode);
        // if (activeAppResult.GetNonNullOrThrow()) return Result<BuilderApplicationForm>.Failure("You already have an active application under review. You cannot start a new application until your current one has been reviewed.");
        //
        // application = new BuilderApplicationForm(user, discordId);
        // Applications[discordId] = application;
        //
        // return Result<BuilderApplicationForm>.Success(application);
    }

    public bool HasApplicationInProgress(ulong discordId)
    {
        return Applications.ContainsKey(discordId);
    }

    public async Task<Result<bool>> HasApplicationUnderReview(ulong discordId)
    {
        var userReferencesResult = await gfApiService.GetUsersConnectedToDiscordAccount(discordId);
        if (!userReferencesResult.TryGetDataNonNull(out var userReferences))
            return Result<bool>.Failure(userReferencesResult.ErrorMessage ?? "Failed to retrieve user references.", userReferencesResult.StatusCode);
        foreach (var userReference in userReferences.Users)
        {
            var applicationsResult = await gfApiService.GetApplicationsByUser(userReference.UserId);
            if (!applicationsResult.TryGetDataNonNull(out var appResults))
                return Result<bool>.Failure(applicationsResult.ErrorMessage ?? "Failed to retrieve user applications.", applicationsResult.StatusCode);
            
            if (appResults.Any(result => result.LatestStatus is null || result.LatestStatus.Status.Equals("UnderReview", StringComparison.OrdinalIgnoreCase) || result.LatestStatus.Status.Equals("SubmissionPending", StringComparison.OrdinalIgnoreCase)))
                return Result<bool>.Success(true);
        }
        return Result<bool>.Success(false);
    }

    public async Task<Result<long>> SubmitApplication(ulong discordId)
    {
        if (!Applications.TryGetValue(discordId, out var application))
            return Result<long>.Failure("No in-progress application found for this user.");
        
        var submitResult = await gfApiService.SubmitApplication(application);
        if (!submitResult.TryGetDataNonNull(out var submittedAppId))
            return Result<long>.Failure(submitResult.ErrorMessage ?? "Failed to submit application.", submitResult.StatusCode);
        
        var statusResult = await gfApiService.AddApplicationStatus(submittedAppId, "SubmissionPending", null);
        if (!statusResult.TryGetDataNonNull(out var wasAdded) || !wasAdded)
            return Result<long>.Failure(statusResult.ErrorMessage ?? "Failed to set application status.", statusResult.StatusCode);
        
        Applications.Remove(discordId);
        return Result<long>.Success(submittedAppId);
    }

    private async Task<(Stream ImageData, ApplicationImage ApplicationImage)> DownloadImageAsync(ApplicationImage image)
    {
        using var httpClient = new HttpClient();
        return (await httpClient.GetStreamAsync(image.Link), image);
    }

    private async Task<Result<List<(string AttachmentName, Stream ImageData, ApplicationImage ApplicationImage)>>> DownloadImagesAsync(List<ApplicationImage> images)
    {
        const int maxConcurrentDownloads = 4;
        var semaphore = new SemaphoreSlim(maxConcurrentDownloads);
        var currentImageIndex = 0;

        try
        {
            var downloadTasks = images.Select(image => Task.Run(async () =>
                {
                    try
                    {
                        await semaphore.WaitAsync();
                        var download = await DownloadImageAsync(image);
                        var extension = Path.GetExtension(new Uri(image.Link).AbsolutePath);
                        var fileIndex = Interlocked.Increment(ref currentImageIndex) - 1;
                        return ($"image-{fileIndex}{extension}", download.ImageData, download.ApplicationImage);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }))
                .ToList();

            var res = await Task.WhenAll(downloadTasks);
            return Result<List<(string AttachmentName, Stream ImageData, ApplicationImage ApplicationImage)>>.Success(res.ToList());
        }
        catch (Exception e)
        {
            return Result<List<(string AttachmentName, Stream ImageData, ApplicationImage ApplicationImage)>>.Failure($"An error occurred while downloading images: {e.Message}", HttpStatusCode.InternalServerError);
        }
        finally
        {
            semaphore.Dispose();
        }
    }
    
    public async Task<Result> CompleteAndForwardApplicationToReview(ulong discordSnowflake, Application appToForward)
    {
        var forwardChannelId = options.Value.ReviewChannelId;
        
        try
        {
            var downloadedImagesResult = await DownloadImagesAsync(appToForward.Images);
            if (!downloadedImagesResult.TryGetDataNonNull(out var downloadedImages))
            {
                await restClient.SendMessageAsync(forwardChannelId,
                    new MessageProperties()
                        .WithEmbeds([
                            GenericEmbeds.InternalError("Greenfield Application Service",
                                $"Failed to download application images for application ID: {appToForward.ApplicationId}. Error: {downloadedImagesResult.ErrorMessage}")
                        ])
                );
                return Result.Failure(downloadedImagesResult.ErrorMessage ?? "Failed to download application images.", downloadedImagesResult.StatusCode);
            }
            
            var appSummary = await BuildApplicationSummary(discordSnowflake, appToForward, overrideImages: downloadedImages.Select(di =>
            {
                var imageId = di.ApplicationImage.ImageLinkId;
                var type = di.ApplicationImage.ImageType;
                var attachmentUrl = $"attachment://{di.AttachmentName}";
                return new ApplicationImage(imageId, attachmentUrl, type, di.ApplicationImage.CreatedOn);
            }).ToList());
            
            var applicationMessage = await restClient.SendMessageAsync(forwardChannelId,
                new MessageProperties()
                    .WithComponents([appSummary.WithAccentColor(ColorHelpers.Info)])
                    .WithFlags(MessageFlags.IsComponentsV2)
                    .WithAttachments(downloadedImages.Select(data => new AttachmentProperties(data.AttachmentName, data.ImageData)))
            );

            IEnumerable<(string ImageName, string Url)> images = applicationMessage.Components
                .OfType<ComponentContainer>()
                .First().Components
                .OfType<MediaGallery>()
                .SelectMany(mg => mg.Items)
                .Select(mgi =>
                {
                    var url = mgi.Media.Url;
                    var imageName = Path.GetFileName(new Uri(url).AbsolutePath);
                    return (imageName, url);
                });
            
            // Update image links in GreenfieldApi to point to the non-ephemeral Discord attachments
            var attachmentsToUpdate = downloadedImages
                .Join(images, 
                    appImg => appImg.AttachmentName, 
                    msgAtt => msgAtt.ImageName, 
                    (img, att) => 
                        img.ApplicationImage with { Link = att.Url, ImageType = img.ApplicationImage.ImageType.Replace("Temp", "") })
                .ToList();
            var updateImageTasks = attachmentsToUpdate.Select(img => gfApiService.UpdateApplicationImage(img.ImageLinkId, img.Link, img.ImageType)).ToList();
            var updateResults = await Task.WhenAll(updateImageTasks);
            var failedUpdates = updateResults.Where(r => !r.IsSuccessful).ToList();
            if (failedUpdates.Count > 0)
            {
                var errorMessages = string.Join("; ", failedUpdates.Select(r => r.ErrorMessage));
                await restClient.SendMessageAsync(forwardChannelId,
                    new MessageProperties()
                        .WithEmbeds([
                            GenericEmbeds.InternalError("Greenfield Application Service",
                                $"Failed to update {failedUpdates.Count} application image links for application ID: {appToForward.ApplicationId}. Errors: {errorMessages}")
                        ])
                );
            }
            
            var statusUpdateResult = await gfApiService.AddApplicationStatus(appToForward.ApplicationId, "UnderReview", null);
            if (!statusUpdateResult.IsSuccessful || !statusUpdateResult.GetNonNullOrThrow())
            {
                await restClient.SendMessageAsync(forwardChannelId,
                    new MessageProperties()
                        .WithEmbeds([
                            GenericEmbeds.InternalError("Greenfield Application Service",
                                $"Failed to set application status to UnderReview for application ID: {appToForward.ApplicationId}. Error: {statusUpdateResult.ErrorMessage}")
                        ])
                );
            }

            await applicationMessage.AddReactionAsync(new ReactionEmojiProperties("✅"));
            await Task.Delay(500);
            await applicationMessage.AddReactionAsync(new ReactionEmojiProperties("❌"));
            return Result<bool>.Success(true);
        } catch (Exception e)
        {
            try
            {
                await restClient.SendMessageAsync(forwardChannelId,
                    new MessageProperties().WithEmbeds([
                        GenericEmbeds.UserError("Greenfield Application Service",
                            $"Failed to forward application ID: {appToForward.ApplicationId}. Error: {e.Message}")
                    ]));
            }
            catch (Exception e2)
            {
                return Result<bool>.Failure($"An error occurred while forwarding the application to review, and the error notification also failed: {e2.Message}. Your application ID is: #{appToForward.ApplicationId}", HttpStatusCode.InternalServerError);
            }
            return Result<bool>.Failure($"An error occurred while forwarding the application to review: {e.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<ComponentContainerProperties> BuildApplicationSummary(ulong discordSnowflake, Application appToForward, bool includeButtons = true, bool onlyShowBasicInfo = false, List<ApplicationImage>? overrideImages = null)
    {
        var userResult = await gfApiService.GetUserById(appToForward.UserId);
        if (!userResult.TryGetDataNonNull(out var user))
            throw new Exception($"Failed to retrieve user data for application summary: {userResult.ErrorMessage}");
        
        var applicationComponents = new List<IComponentContainerComponentProperties>();
        applicationComponents.Add(new TextDisplayProperties($"# Builder Application Summary #{appToForward.ApplicationId}"));
        if (onlyShowBasicInfo) 
            applicationComponents.Add(new TextDisplayProperties($"**__Discord__**: <@{discordSnowflake}>\t\t**__Minecraft IGN__**: `{user.Username}`\t\t**__Age__**: `{appToForward.Age}`{(appToForward.Nationality != null ? $"\t\t**__Nationality__**: `{appToForward.Nationality}`" : "")}"));
        else
        {
            var imagesToUse = overrideImages ?? appToForward.Images;
            var houseImageLinks = imagesToUse
                .Where(img =>
                    img.ImageType.Equals("TempHouse", StringComparison.OrdinalIgnoreCase) ||
                    img.ImageType.Equals("House", StringComparison.OrdinalIgnoreCase))
                .Select(img => img.Link);
            
            var otherImageLinks = imagesToUse
                .Where(img =>
                    img.ImageType.Equals("TempOther", StringComparison.OrdinalIgnoreCase) ||
                    img.ImageType.Equals("Other", StringComparison.OrdinalIgnoreCase))
                .Select(img => img.Link).ToList();
            
            applicationComponents.Add(new TextDisplayProperties($"**__Discord__**: <@{discordSnowflake}>\t\t**__Minecraft IGN__**: `{user.Username}`\t\t**__Age__**: `{appToForward.Age}`{(appToForward.Nationality != null ? $"\t\t**__Nationality__**: `{appToForward.Nationality}`" : "")}\n\n**__Minecraft UUID__**: `{user.MinecraftUuid}`"));
            applicationComponents.Add(new ComponentSeparatorProperties());
            applicationComponents.Add(new TextDisplayProperties("### Why do you want to be a part of Greenfield?"));
            applicationComponents.Add(new TextDisplayProperties(appToForward.WhyJoinGreenfield));
            applicationComponents.Add(new ComponentSeparatorProperties());
            applicationComponents.Add(new TextDisplayProperties("### North American House Builds"));
            applicationComponents.Add(new MediaGalleryProperties().WithItems(houseImageLinks.Select(link => new MediaGalleryItemProperties(new ComponentMediaProperties(link)))));
            if (otherImageLinks.Count != 0)
            {
                applicationComponents.Add(new ComponentSeparatorProperties());
                applicationComponents.Add(new TextDisplayProperties("### Other Builds"));
                applicationComponents.Add(new MediaGalleryProperties().WithItems(otherImageLinks.Select(link => new MediaGalleryItemProperties(new ComponentMediaProperties(link)))));
            }

            if (!string.IsNullOrWhiteSpace(appToForward.AdditionalBuildingInformation))
            {
                applicationComponents.Add(new ComponentSeparatorProperties());
                applicationComponents.Add(new TextDisplayProperties("### Additional Building Information"));
                applicationComponents.Add(new TextDisplayProperties(appToForward.AdditionalBuildingInformation));
            }

            if (!string.IsNullOrWhiteSpace(appToForward.AdditionalComments))
            {
                applicationComponents.Add(new ComponentSeparatorProperties());
                applicationComponents.Add(new TextDisplayProperties("### Additional Comments"));
                applicationComponents.Add(new TextDisplayProperties(appToForward.AdditionalComments));
            }   
        }
        
        if (!includeButtons) return new ComponentContainerProperties(applicationComponents);
        
        applicationComponents.Add(new ComponentSeparatorProperties());
        applicationComponents.Add(new ActionRowProperties([
            new ButtonProperties("buildapp_approve_button", "Approve", ButtonStyle.Primary),
            new ButtonProperties("buildapp_reject_button", "Reject", ButtonStyle.Secondary)
        ]));

        return new ComponentContainerProperties(applicationComponents);
    }

    // public async Task<Result<bool>> ForwardApplicationToReview(ulong discordSnowflake, Application appToForward)
    // {
    //     var forwardChannelId = options.Value.ReviewChannelId;
    //     
    //     try
    //     {
    //         var appSummary = await BuildApplicationSummary(discordSnowflake, appToForward);
    //         var applicationMessage = await restClient.SendMessageAsync(forwardChannelId,
    //             new MessageProperties()
    //                 .WithComponents([appSummary.WithAccentColor(ColorHelpers.Info)])
    //                 .WithFlags(MessageFlags.IsComponentsV2)
    //         );
    //
    //         await applicationMessage.AddReactionAsync(new ReactionEmojiProperties("✅"));
    //         await Task.Delay(100);
    //         await applicationMessage.AddReactionAsync(new ReactionEmojiProperties("❌"));
    //         return Result<bool>.Success(true);
    //     } catch (Exception e)
    //     {
    //         try
    //         {
    //             await restClient.SendMessageAsync(forwardChannelId,
    //                 new MessageProperties().WithEmbeds([
    //                     GenericEmbeds.UserError("Greenfield Application Service",
    //                         $"Failed to forward application ID: {appToForward.ApplicationId}")
    //                 ]));
    //             Result<bool>.Success(false);
    //         }
    //         catch (Exception e2)
    //         {
    //             return Result<bool>.Failure($"An error occurred while forwarding the application to review, and the error notification also failed: {e2.Message}. Your application ID is: #{appToForward.ApplicationId}", HttpStatusCode.InternalServerError);
    //         }
    //         return Result<bool>.Failure($"An error occurred while forwarding the application to review: {e.Message}", HttpStatusCode.InternalServerError);
    //     }
    // }

    public async Task<Result<ulong>> DenyApplication(ulong discordSnowflake, Application appToDeny, string reason)
    {
        try
        {
            var userResult = await gfApiService.GetUserById(appToDeny.UserId);
            if (!userResult.TryGetDataNonNull(out var user))
                throw new Exception($"Failed to retrieve user data for application summary: {userResult.ErrorMessage}");
            
            var storageChannel = options.Value.ApplicationStorageChannelId;
            var summary = await BuildApplicationSummary(discordSnowflake, appToDeny, includeButtons: false);

            var statusSetResult = await gfApiService.AddApplicationStatus(appToDeny.ApplicationId, "Rejected", reason);
            if (!statusSetResult.IsSuccessful || !statusSetResult.GetNonNullOrThrow())
                return Result<ulong>.Failure($"Failed to set application status: {statusSetResult.ErrorMessage}", statusSetResult.StatusCode);
            
            var denialEmbed = new EmbedProperties()
                .WithTitle("About your application...")
                .WithDescription(
                    $"Thank you for taking interest in joining The Greenfield Project. Unfortunately, we regret to inform you that your application was not approved. This could have been due to various reasons - we have outlined some of (if not all) of the reasons below:\n\n{reason}")
                .WithColor(ColorHelpers.Failure);

            var dmChannelTask = restClient.GetDMChannelAsync(discordSnowflake);
            var channel = await restClient.CreateForumGuildThreadAsync(storageChannel,
                new ForumGuildThreadProperties($"❌ App-{appToDeny.ApplicationId} | {user.Username}", 
                        new ForumGuildThreadMessageProperties()
                            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(discordSnowflake))
                            .WithComponents([summary.WithAccentColor(ColorHelpers.Info)])
                            .WithFlags(MessageFlags.IsComponentsV2))
                    .WithAutoArchiveDuration(ThreadArchiveDuration.OneDay)
                );

            await Task.Delay(100);
            
            _ = restClient.SendMessageAsync(channel.Id,
                new MessageProperties()
                    .WithEmbeds([GenericEmbeds.Info("Application Denied", $"Reasons: \n\n{reason}")])
                    .WithMessageReference(MessageReferenceProperties.Reply(channel.Id, false))
                    .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(discordSnowflake))
            );
            
            var dmChannel = await dmChannelTask;
            var userSummaryMsg = await restClient.SendMessageAsync(dmChannel.Id, new MessageProperties()
                .WithComponents([summary.WithAccentColor(ColorHelpers.Failure)])
                .WithFlags(MessageFlags.IsComponentsV2)
                .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(discordSnowflake))
            );
            
            await restClient.SendMessageAsync(dmChannel.Id,
                new MessageProperties()
                    .WithContent($"<@{discordSnowflake}>")
                    .WithEmbeds([denialEmbed])
                    .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(discordSnowflake))
                    .WithMessageReference(MessageReferenceProperties.Reply(userSummaryMsg.Id, false))
                
            );

            return Result<ulong>.Success(channel.Id);
        }
        catch (Exception e)
        {
            return Result<ulong>.Failure($"An error occurred while denying the application: {e.Message}");
        }
    }

    public async Task<Result<ulong>> AcceptApplication(ulong discordSnowflake, Application appToAccept,
        string? comments)
    {
        try
        {
            var userResult = await gfApiService.GetUserById(appToAccept.UserId);
            if (!userResult.TryGetDataNonNull(out var user))
                throw new Exception($"Failed to retrieve user data for application summary: {userResult.ErrorMessage}");
            
            var storageChannel = options.Value.ApplicationStorageChannelId;
            var testBuilderChannel = options.Value.TestBuilderChannelId;
            var testRoleId = options.Value.TestBuildRoleId;
            var guildId = options.Value.GuildId;
            
            var summary = await BuildApplicationSummary(discordSnowflake, appToAccept, includeButtons: false);
            
            var statusSetResult = await gfApiService.AddApplicationStatus(appToAccept.ApplicationId, "Approved", comments ?? "No additional comments.");
            if (!statusSetResult.IsSuccessful || !statusSetResult.GetNonNullOrThrow())
                return Result<ulong>.Failure($"Failed to set application status: {statusSetResult.ErrorMessage}", statusSetResult.StatusCode);
            
            var acceptanceEmbed = new EmbedProperties()
                .WithTitle("Congratulations! Your application has been approved!")
                .WithDescription(
                    $"Welcome to The Greenfield Project! If you aren't whitelisted already, you should be shortly. Please read through the pinned messages for your next steps!")
                .WithColor(ColorHelpers.Success);

            var channel = await restClient.CreateForumGuildThreadAsync(storageChannel,
                new ForumGuildThreadProperties($"✅ App-{appToAccept.ApplicationId} | {user.Username}", 
                        new ForumGuildThreadMessageProperties()
                            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(discordSnowflake))
                            .WithComponents([summary.WithAccentColor(ColorHelpers.Info)])
                            .WithFlags(MessageFlags.IsComponentsV2))
                    .WithAutoArchiveDuration(ThreadArchiveDuration.OneDay)
                );
            
            _ = restClient.SendMessageAsync(channel.Id,
                new MessageProperties()
                    .WithEmbeds([GenericEmbeds.Info("Application Accepted", $"The applicant has been approved. Additional comments (if any): \n\n{comments ?? "None provided."}")])
                    .WithMessageReference(MessageReferenceProperties.Reply(channel.Id, false))
                    .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(discordSnowflake))
            );

            await Task.Delay(100);

            await restClient.SendMessageAsync(testBuilderChannel, new MessageProperties().WithEmbeds([acceptanceEmbed]));
            await restClient.AddGuildUserRoleAsync(guildId, discordSnowflake, testRoleId, new RestRequestProperties().WithAuditLogReason("Builder application accepted"));
            
            return Result<ulong>.Success(channel.Id);
        }
        catch (Exception e)
        {
            return Result<ulong>.Failure($"An error occurred while accepting the application: {e.Message}");
        }
    }
}