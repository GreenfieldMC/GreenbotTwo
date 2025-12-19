using System.Collections.Concurrent;
using System.Net;
using GreenbotTwo.Configuration.Models;
using GreenbotTwo.Embeds;
using GreenbotTwo.Models;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;

namespace GreenbotTwo.Services;

public class BuilderApplicationService(IOptions<BuilderApplicationSettings> options, RestClient restClient, IGreenfieldApiService gfApiService) : IApplicationService<BuilderApplicationForm, BuilderApplication>
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

    public async Task<Result<BuilderApplicationForm>> GetOrStartApplication(ulong discordId)
    {
        if (Applications.TryGetValue(discordId, out var application)) return Result<BuilderApplicationForm>.Success(application);
        
        var activeAppResult = await HasApplicationUnderReview(discordId);
        if (!activeAppResult.IsSuccessful) return Result<BuilderApplicationForm>.Failure(activeAppResult.ErrorMessage ?? "Failed to retrieve application status.", activeAppResult.StatusCode);
        if (activeAppResult.GetNonNullOrThrow()) return Result<BuilderApplicationForm>.Failure("You already have an active application under review. You cannot start a new application until your current one has been reviewed.");
        
        application = new BuilderApplicationForm(discordId);
        Applications[discordId] = application;
        
        return Result<BuilderApplicationForm>.Success(application);
    }

    public bool HasApplicationInProgress(ulong discordId)
    {
        return Applications.ContainsKey(discordId);
    }

    public async Task<Result<bool>> HasApplicationUnderReview(ulong discordId)
    {
        var userReferencesResult = await gfApiService.GetUsersByDiscordSnowflake(discordId);
        if (!userReferencesResult.TryGetDataNonNull(out var userReferences))
            return Result<bool>.Failure(userReferencesResult.ErrorMessage ?? "Failed to retrieve user references.", userReferencesResult.StatusCode);
        foreach (var userReference in userReferences)
        {
            var applicationsResult = await gfApiService.GetBuilderApplicationsByUser(userReference.UserId);
            if (!applicationsResult.TryGetDataNonNull(out var appResults))
                return Result<bool>.Failure(applicationsResult.ErrorMessage ?? "Failed to retrieve user applications.", applicationsResult.StatusCode);
            
            if (appResults.Any(result => result.LatestStatus is null || result.LatestStatus.Status.Equals("UnderReview", StringComparison.OrdinalIgnoreCase)))
                return Result<bool>.Success(true);
        }
        return Result<bool>.Success(false);
    }

    public async Task<Result<long>> SubmitApplication(ulong discordId)
    {
        if (!Applications.TryGetValue(discordId, out var application))
            return Result<long>.Failure("No in-progress application found for this user.");
        
        var submitResult = await gfApiService.SubmitBuilderApplication(application);
        if (!submitResult.TryGetDataNonNull(out var submittedAppId))
            return Result<long>.Failure(submitResult.ErrorMessage ?? "Failed to submit application.", submitResult.StatusCode);
        
        var statusResult = await gfApiService.AddApplicationStatus(submittedAppId, "UnderReview", null);
        if (!statusResult.TryGetDataNonNull(out var wasAdded) || !wasAdded)
            return Result<long>.Failure(statusResult.ErrorMessage ?? "Failed to set application status.", statusResult.StatusCode);
        
        Applications.Remove(discordId);
        return Result<long>.Success(submittedAppId);
    }

    public Task<Result<BuilderApplication>> GetSubmittedApplicationById(long applicationId)
    {
        return gfApiService.GetBuilderApplicationById(applicationId);
    }

    public async Task<ComponentContainerProperties> BuildApplicationSummary(ulong discordSnowflake,
        BuilderApplication appToForward, bool includeButtons = true, bool onlyShowBasicInfo = false)
    {
        var applicationComponents = new List<IComponentContainerComponentProperties>();
        applicationComponents.Add(new TextDisplayProperties($"# Builder Application Summary #{appToForward.ApplicationId}"));
        if (onlyShowBasicInfo) 
            applicationComponents.Add(new TextDisplayProperties($"**__Discord__**: <@{discordSnowflake}>\t\t**__Minecraft IGN__**: `{appToForward.User.Username}`\t\t**__Age__**: `{appToForward.Age}`{(appToForward.Nationality != null ? $"\t\t**__Nationality__**: `{appToForward.Nationality}`" : "")}"));
        else
        {
            applicationComponents.Add(new TextDisplayProperties($"**__Discord__**: <@{discordSnowflake}>\t\t**__Minecraft IGN__**: `{appToForward.User.Username}`\t\t**__Age__**: `{appToForward.Age}`{(appToForward.Nationality != null ? $"\t\t**__Nationality__**: `{appToForward.Nationality}`" : "")}\n\n**__Minecraft UUID__**: `{appToForward.User.MinecraftUuid}`"));
            applicationComponents.Add(new ComponentSeparatorProperties());
            applicationComponents.Add(new TextDisplayProperties("### Why do you want to be a part of Greenfield?"));
            applicationComponents.Add(new TextDisplayProperties(appToForward.WhyJoinGreenfield));
            applicationComponents.Add(new ComponentSeparatorProperties());
            applicationComponents.Add(new TextDisplayProperties("### North American House Builds"));
            applicationComponents.Add(new MediaGalleryProperties().WithItems(appToForward.HouseBuilds.Select(link => new MediaGalleryItemProperties(new ComponentMediaProperties(link.Link)))));
            if (appToForward.OtherBuilds.Count != 0)
            {
                applicationComponents.Add(new ComponentSeparatorProperties());
                applicationComponents.Add(new TextDisplayProperties("### Other Builds"));
                applicationComponents.Add(new MediaGalleryProperties().WithItems(appToForward.OtherBuilds.Select(link => new MediaGalleryItemProperties(new ComponentMediaProperties(link.Link)))));
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

    public async Task<Result<bool>> ForwardApplicationToReview(ulong discordSnowflake, BuilderApplication appToForward)
    {
        var forwardChannelId = options.Value.ReviewChannelId;
        
        try
        {
            var appSummary = await BuildApplicationSummary(discordSnowflake, appToForward);
            var applicationMessage = await restClient.SendMessageAsync(forwardChannelId,
                new MessageProperties()
                    .WithComponents([appSummary.WithAccentColor(new Color(50, 127, 168))])
                    .WithFlags(MessageFlags.IsComponentsV2)
            );

            await applicationMessage.AddReactionAsync(new ReactionEmojiProperties("✅"));
            await Task.Delay(100);
            await applicationMessage.AddReactionAsync(new ReactionEmojiProperties("❌"));
            return Result<bool>.Success(true);
        } catch (Exception e)
        {
            try
            {
                await restClient.SendMessageAsync(forwardChannelId,
                    new MessageProperties().WithEmbeds([
                        GenericEmbeds.UserError("Greenfield Application Service",
                            $"Failed to forward application ID: {appToForward.ApplicationId}")
                    ]));
                Result<bool>.Success(false);
            }
            catch (Exception e2)
            {
                return Result<bool>.Failure($"An error occurred while forwarding the application to review, and the error notification also failed: {e2.Message}. Your application ID is: #{appToForward.ApplicationId}", HttpStatusCode.InternalServerError);
            }
            return Result<bool>.Failure($"An error occurred while forwarding the application to review: {e.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<ulong>> DenyApplication(ulong discordSnowflake, BuilderApplication appToDeny, string reason)
    {
        try
        {
            var storageChannel = options.Value.ApplicationStorageChannelId;
            var summary = await BuildApplicationSummary(discordSnowflake, appToDeny, includeButtons: false);

            var statusSetResult = await gfApiService.AddApplicationStatus(appToDeny.ApplicationId, "Rejected", reason);
            if (!statusSetResult.IsSuccessful || !statusSetResult.GetNonNullOrThrow())
                return Result<ulong>.Failure($"Failed to set application status: {statusSetResult.ErrorMessage}", statusSetResult.StatusCode);
            
            var denialEmbed = new EmbedProperties()
                .WithTitle("About your application...")
                .WithDescription(
                    $"Thank you for taking interest in joining The Greenfield Project. Unfortunately, we regret to inform you that your application was not approved. This could have been due to various reasons - we have outlined some of (if not all) of the reasons below.\n\n{reason}")
                .WithColor(ColorHelpers.Failure);

            var dmChannelTask = restClient.GetDMChannelAsync(discordSnowflake);
            var channel = await restClient.CreateForumGuildThreadAsync(storageChannel,
                new ForumGuildThreadProperties($"❌ App-{appToDeny.ApplicationId} | {appToDeny.User.Username}", 
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

    public async Task<Result<ulong>> AcceptApplication(ulong discordSnowflake, BuilderApplication appToAccept, string? comments)
    {
        try
        {
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
                new ForumGuildThreadProperties($"✅ App-{appToAccept.ApplicationId} | {appToAccept.User.Username}", 
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