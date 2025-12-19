using GreenbotTwo.Models;
using NetCord.Rest;

namespace GreenbotTwo.Services.Interfaces;

/// <summary>
/// Service for managing user applications.
/// </summary>
/// <typeparam name="TForm">The application form before submission.</typeparam>
/// <typeparam name="TSubmitted">The submitted application type.</typeparam>
public interface IApplicationService<TForm, in TSubmitted>
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
    IEnumerable<TForm> GetInProgressApplications();

    /// <summary>
    /// Get or start an application for the given discord user. If there is an active (not submitted) application already, it will be returned instead. If the user has a submitted application, this will return null.
    /// </summary>
    /// <param name="discordId">The discord user who is applying</param>
    /// <returns></returns>
    Task<Result<TForm>> GetOrStartApplication(ulong discordId);
    
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
    /// Submit an application for the given discord user.
    /// </summary>
    /// <param name="discordId">The discord user who is submitting the application</param>
    /// <returns>True if the application was successfully submitted; false otherwise.</returns>
    Task<Result<long>> SubmitApplication(ulong discordId);

    /// <summary>
    /// Get an application by its ID.
    /// </summary>
    /// <param name="applicationId"></param>
    /// <returns></returns>
    Task<Result<BuilderApplication>> GetSubmittedApplicationById(long applicationId);

    /// <summary>
    /// Build a summary of the application to be forwarded for review as a component container.
    /// </summary>
    /// <param name="discordSnowflake">The discord user who submitted the application</param>
    /// <param name="appToForward">The application to build the summary for</param>
    /// <param name="includeButtons">Whether to include action buttons in the summary</param>
    /// <param name="onlyShowBasicInfo"></param>
    /// <returns></returns>
    Task<ComponentContainerProperties> BuildApplicationSummary(ulong discordSnowflake, TSubmitted appToForward,
        bool includeButtons = true, bool onlyShowBasicInfo = false);

    /// <summary>
    /// Forward the given application to review.
    /// </summary>
    /// <param name="discordSnowflake">The discord user who submitted the application</param>
    /// <param name="appToForward">The application to forward to review</param>
    /// <returns>>True if the application was successfully forwarded; false otherwise.</returns>
    Task<Result<bool>> ForwardApplicationToReview(ulong discordSnowflake, TSubmitted appToForward);

    /// <summary>
    /// Deny the given application.
    /// </summary>
    /// <param name="discordSnowflake">The user to be notified of the denial</param>
    /// <param name="appToDeny">The application to deny</param>
    /// <param name="reason">The reason for the denial</param>
    /// <returns></returns>
    Task<Result<ulong>> DenyApplication(ulong discordSnowflake, TSubmitted appToDeny, string reason);

    /// <summary>
    /// Accept the given application.
    /// </summary>
    /// <param name="discordSnowflake">The user to be notified of the acceptance</param>
    /// <param name="appToAccept">The application to accept</param>
    /// <param name="comments">Optional comments to save in the application database.</param>
    /// <returns></returns>
    Task<Result<ulong>> AcceptApplication(ulong discordSnowflake, TSubmitted appToAccept, string? comments);

}