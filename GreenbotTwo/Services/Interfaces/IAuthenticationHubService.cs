using GreenbotTwo.Models;
using GreenbotTwo.Models.AuthHub;

namespace GreenbotTwo.Services.Interfaces;

public interface IAuthenticationHubService
{
    /// <summary>
    /// Validates the given Minecraft username via Authentication Hub service.
    /// </summary>
    /// <param name="minecraftUsername"></param>
    /// <returns></returns>
    Task<Result> ValidateUsername(string minecraftUsername);

    /// <summary>
    /// Authorizes the given Minecraft username and auth code via Authentication Hub service.
    /// </summary>
    /// <param name="minecraftUsername"></param>
    /// <param name="authCode">The auth code given to the user from the Authentication Hub service.</param>
    /// <returns>URL to call to get applications. Does not include the base url</returns>
    Task<Result<string>> Authorize(string minecraftUsername, string authCode);

    /// <summary>
    /// Removes the auth session for the given Minecraft UUID via Authentication Hub service.
    /// </summary>
    /// <param name="minecraftUuid"></param>
    /// <returns></returns>
    Task<Result> RemoveAuthSession(Guid minecraftUuid);

}