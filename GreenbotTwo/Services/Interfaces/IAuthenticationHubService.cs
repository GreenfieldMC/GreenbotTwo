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
    /// Gets the applications associated with the given state from Authentication Hub service.
    /// </summary>
    /// <param name="applicationCallUrl">URL to call to get applications. Should not include the base url</param>
    /// <returns>A list of Application connections and their statuses.</returns>
    Task<Result<AuthHubAppConnections>> GetApplications(string applicationCallUrl);
}