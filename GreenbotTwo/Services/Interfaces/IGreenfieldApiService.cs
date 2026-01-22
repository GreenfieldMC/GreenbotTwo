using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;

namespace GreenbotTwo.Services.Interfaces;

public interface IGreenfieldApiService
{
    /// <summary>
    /// /user/{minecraftUuid} [PUT]
    /// </summary>
    /// <param name="minecraftUuid"></param>
    /// <param name="username"></param>
    /// <returns></returns>
    Task<Result<User>> CreateUser(Guid minecraftUuid, string username);
    
    /// <summary>
    /// /buildcodes/all [GET]
    /// </summary>
    /// <returns></returns>
    Task<Result<IEnumerable<BuildCode>>> GetBuildCodes();
    
    /// <summary>
    /// /application/submit [POST]
    /// </summary>
    /// <param name="applicationForm"></param>
    /// <returns></returns>
    Task<Result<long>> SubmitApplication(BuilderApplicationForm applicationForm);

    /// <summary>
    /// /application/images/{imageLinkId} [PUT]
    /// </summary>
    /// <param name="imageLinkId"></param>
    /// <param name="newImageUrl"></param>
    /// <param name="newImageType"></param>
    /// <returns></returns>
    Task<Result> UpdateApplicationImage(long imageLinkId, string newImageUrl, string newImageType);

    /// <summary>
    /// /application/{id}/status [POST]
    /// </summary>
    /// <param name="applicationId"></param>
    /// <param name="status"></param>
    /// <param name="statusMessage"></param>
    /// <returns></returns>
    Task<Result<ApplicationStatus>> AddApplicationStatus(long applicationId, string status, string? statusMessage);
    
    /// <summary>
    /// /application/{id} [GET]
    /// </summary>
    /// <param name="applicationId"></param>
    /// <returns></returns>
    Task<Result<Application>> GetApplicationById(long applicationId);
    
    /// <summary>
    /// /user/{id}/applications [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<Result<IEnumerable<LatestApplicationStatus>>> GetApplicationsByUser(long userId);
    
    /// <summary>
    /// /discord/snowflakes/{snowflake}?includeUsers=true [GET]
    /// </summary>
    /// <param name="discordId"></param>
    /// <returns></returns>
    Task<Result<ConnectionModels.ApiDiscordConnectionWithUsers>> GetUsersConnectedToDiscordAccount(ulong discordId);
    
    /// <summary>
    /// /user/{id}/accounts/discord [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<Result<List<ConnectionModels.ApiDiscordAccount>>> GetDiscordAccountsForUser(long userId);
    
    /// <summary>
    /// /discord/oauth/connection-link?redirectUri=...&userId=... [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="redirectUri"></param>
    /// <returns></returns>
    Task<Result<string>> GetDiscordConnectUrl(long userId, string redirectUri);
    
    /// <summary>
    /// /discord/oauth/disconnect-link?redirectUri=...&userId=...&discordConnectionId=... [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="discordConnectionId"></param>
    /// <param name="redirectUri"></param>
    /// <returns></returns>
    Task<Result<string>> GetDiscordDisconnectUrl(long userId, long discordConnectionId, string redirectUri);
    
    /// <summary>
    /// /patreon/oauth/connection-link?redirectUri=...&userId=... [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="redirectUri"></param>
    /// <returns></returns>
    Task<Result<string>> GetPatreonConnectUrl(long userId, string redirectUri);

    /// <summary>
    /// /patreon/oauth/disconnect-link?redirectUri=...&userId=...&patreonConnectionId=... [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="patreonConnectionId"></param>
    /// <param name="redirectUri"></param>
    /// <returns></returns>
    Task<Result<string>> GetPatreonDisconnectUrl(long userId, long patreonConnectionId, string redirectUri);

    /// <summary>
    /// /user/{id} [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<Result<User>> GetUserById(long userId);
    
    /// <summary>
    /// /user/{minecraftUuid} [GET]
    /// </summary>
    /// <param name="minecraftUuid"></param>
    /// <returns></returns>
    Task<Result<User>> GetUserByMinecraftUuid(Guid minecraftUuid);

    /// <summary>
    /// /patreon/accounts/{id}/users?includeUsers=true [GET]
    /// </summary>
    /// <param name="patreonConnectionId"></param>
    /// <returns></returns>
    Task<Result<ConnectionModels.ApiPatreonConnectionWithUsers>> GetUsersConnectedToPatreonAccount(long patreonConnectionId);
    
    /// <summary>
    /// /user/{id}/accounts/patreon [GET]
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<Result<List<ConnectionModels.ApiPatreonAccount>>> GetPatronAccountsForUser(long userId);

}