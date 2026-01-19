using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
using NetCord;
using NetCord.Rest;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;
using User = GreenbotTwo.Models.GreenfieldApi.User;

namespace GreenbotTwo.Services.Interfaces;

/// <summary>
/// Service for managing user build applications.
/// </summary>
public interface IApplicationService
{
    
    /// <summary>
    /// Clear any in-progress application for the given discord user.
    /// </summary>
    /// <param name="discordId">The discord user whose in-progress application should be cleared</param>
    /// <returns>>True if an in-progress application was found and cleared; false otherwise.</returns>
    bool ClearInProgressApplication(ulong discordId);
    
    /// <summary>
    /// Get all applications that are currently in progress (not submitted yet).
    /// </summary>
    /// <returns></returns>
    IEnumerable<BuilderApplicationForm> GetInProgressApplications();

    /// <summary>
    /// Start a new application for the given discord user. If an application is already in progress, it will be overwritten.
    /// </summary>
    /// <param name="discordSnowflake">The discord user who is applying</param>
    /// <param name="user">The user object associated with the discord user.</param>
    /// <returns></returns>
    BuilderApplicationForm StartApplication(ulong discordSnowflake, User user);

    /// <summary>
    /// Get the in progress application for the given discord user.
    /// </summary>
    /// <param name="discordId">The discord user who is applying</param>
    /// <returns></returns>
    BuilderApplicationForm? GetApplication(ulong discordId);
    
    /// <summary>
    /// Check if a discord user has an application that is in progress (not submitted yet).
    /// </summary>
    /// <param name="discordId"></param>
    /// <returns></returns>
    bool HasApplicationInProgress(ulong discordId);
    
    /// <summary>
    /// Check if a discord user has an active application (an application that has been submitted and has not yet been reviewed).
    /// </summary>
    /// <param name="discordId">The discord user to check</param>
    /// <returns>>True if the user has an active application; false otherwise.</returns>
    Task<Result<bool>> HasApplicationUnderReview(ulong discordId);

    /// <summary>
    /// Submit an application for the given discord user. The application will be marked as "SubmissionPending" until it is successfully forwarded for review.
    /// </summary>
    /// <param name="discordId">The discord user who is submitting the application</param>
    /// <returns>True if the application was successfully submitted; false otherwise.</returns>
    Task<Result<long>> SubmitApplication(ulong discordId);

    /// <summary>
    /// Completes an application submission by uploading the images and forwarding the application to review. Also sets the application's status to "UnderReview".
    /// 
    /// NOTE: The application must be in the "SubmissionPending" status to be processed.
    /// </summary>
    /// <param name="discordSnowflake">The discord user who submitted the application</param>
    /// <param name="appToForward">The application to complete and forward</param>
    /// <returns></returns>
    Task<Result> CompleteAndForwardApplicationToReview(ulong discordSnowflake, Application appToForward);

    /// <summary>
    /// Build a summary of the application to be forwarded for review as a component container.
    /// </summary>
    /// <param name="discordSnowflake">The discord user who submitted the application</param>
    /// <param name="appToForward">The application to build the summary for</param>
    /// <param name="includeButtons">Whether to include action buttons in the summary</param>
    /// <param name="onlyShowBasicInfo">Whether to only show basic information in the summary</param>>
    /// <param name="overrideImages">When uploading the images for the first time, they need to be attached like attachments rather than regular links.</param>
    /// <returns></returns>
    Task<ComponentContainerProperties> BuildApplicationSummary(ulong discordSnowflake, Application appToForward, bool includeButtons = true, bool onlyShowBasicInfo = false, List<ApplicationImage>? overrideImages = null);
    //
    // /// <summary>
    // /// Forward the given application to review.
    // /// </summary>
    // /// <param name="discordSnowflake">The discord user who submitted the application</param>
    // /// <param name="appToForward">The application to forward to review</param>
    // /// <returns>>True if the application was successfully forwarded; false otherwise.</returns>
    // Task<Result<bool>> ForwardApplicationToReview(ulong discordSnowflake, TSubmitted appToForward);

    /// <summary>
    /// Deny the given application.
    /// </summary>
    /// <param name="discordSnowflake">The user to be notified of the denial</param>
    /// <param name="appToDeny">The application to deny</param>
    /// <param name="reason">The reason for the denial</param>
    /// <returns></returns>
    Task<Result<ulong>> DenyApplication(ulong discordSnowflake, Application appToDeny, string reason);

    /// <summary>
    /// Accept the given application.
    /// </summary>
    /// <param name="discordSnowflake">The user to be notified of the acceptance</param>
    /// <param name="appToAccept">The application to accept</param>
    /// <param name="comments">Optional comments to save in the application database.</param>
    /// <returns></returns>
    Task<Result<ulong>> AcceptApplication(ulong discordSnowflake, Application appToAccept, string? comments);

}