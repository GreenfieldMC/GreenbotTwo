using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GreenbotTwo.Services;

public class GreenfieldApiService(ILogger<IGreenfieldApiService> logger, HttpClient httpClient) : IGreenfieldApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<User>> CreateUser(Guid minecraftUuid, string username)
    {
        try
        {
            // username needs to go in the body of the request
            var content = new StringContent(JsonSerializer.Serialize(new {username = username}, JsonOptions), Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync($"user/{minecraftUuid}", content);
            
            if (!response.IsSuccessStatusCode)
                return Result<User>.Failure($"Failed to create user. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var user = await JsonSerializer.DeserializeAsync<User>(responseStream, JsonOptions);
            return Result<User>.Success(user!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while creating user with Minecraft UUID {MinecraftUuid}", minecraftUuid);
            return Result<User>.Failure($"An error occurred while creating the user: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<BuildCode>>> GetBuildCodes()
    {
        try
        {
            var response = await httpClient.GetAsync("buildcode/all");
            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<BuildCode>>.Failure($"Failed to fetch build codes. {response.ReasonPhrase ?? ""}", response.StatusCode);
        
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var responseData = JsonSerializer.Deserialize<IEnumerable<BuildCode>>(responseStream, JsonOptions);
            return Result<IEnumerable<BuildCode>>.Success(responseData ?? []);   
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching build codes.");
            return Result<IEnumerable<BuildCode>>.Failure($"An error occurred while fetching build codes: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<long>> SubmitApplication(BuilderApplicationForm applicationForm)
    {
        try
        {
            var submissionModel = new
            {
                UserId = applicationForm.User.UserId,
                Age = applicationForm.Age,
                Nationality = applicationForm.Nationality,
                Images = applicationForm.Images,
                AdditionalBuildingInformation = applicationForm.AdditionalBuildingInformation,
                WhyJoinGreenfield = applicationForm.WhyJoinGreenfield,
                AdditionalComments = applicationForm.AdditionalComments
            };
            
            var content = new StringContent(JsonSerializer.Serialize(submissionModel, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("application/submit", content);
            
            if (!response.IsSuccessStatusCode)
                return Result<long>.Failure($"Failed to submit builder application. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var applicationId = await JsonSerializer.DeserializeAsync<long>(responseStream, JsonOptions);
            return Result<long>.Success(applicationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while submitting the builder application.");
            return Result<long>.Failure($"An error occurred while submitting the builder application: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result> UpdateApplicationImage(long imageLinkId, string newImageUrl, string newImageType)
    {
        try
        {
            var imageModel = new
            {
                ImageLink = newImageUrl,
                ImageType = newImageType
            };
            
            var content = new StringContent(JsonSerializer.Serialize(imageModel, JsonOptions), Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync($"application/images/{imageLinkId}", content);
            
            if (!response.IsSuccessStatusCode) 
                logger.LogWarning("Failed to update application image with ID {ImageLinkId}. Response: {ReasonPhrase}", imageLinkId, response.ReasonPhrase);
            
            return !response.IsSuccessStatusCode 
                ? Result.Failure($"Failed to update application image. {response.ReasonPhrase ?? ""}", response.StatusCode) 
                : Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while updating the application image.");
            return Result.Failure($"An error occurred while updating the application image: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<ApplicationStatus>> AddApplicationStatus(long applicationId, string status, string? statusMessage)
    {
        try 
        {
            var statusModel = new
            {
                Status = status,
                StatusMessage = statusMessage
            };
            
            var content = new StringContent(JsonSerializer.Serialize(statusModel, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"application/{applicationId}/status", content);

            if (!response.IsSuccessStatusCode)
                return Result<ApplicationStatus>.Failure($"Failed to add application status. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var applicationStatus = await JsonSerializer.DeserializeAsync<ApplicationStatus>(responseStream, JsonOptions);
            return Result<ApplicationStatus>.Success(applicationStatus!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while adding application status for application ID {ApplicationId}", applicationId);
            return Result<ApplicationStatus>.Failure($"An error occurred while adding the application status: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Application>> GetApplicationById(long applicationId)
    {
        try
        {
            var response = await httpClient.GetAsync($"application/{applicationId}");

            if (!response.IsSuccessStatusCode)
                return Result<Application>.Failure(
                    $"Failed to fetch builder application. {response.ReasonPhrase ?? ""}", response.StatusCode);

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var application = await JsonSerializer.DeserializeAsync<Application>(responseStream, JsonOptions);
            return Result<Application>.Success(application!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching the builder application with ID {ApplicationId}", applicationId);
            return Result<Application>.Failure($"An error occurred while fetching the builder application: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<LatestApplicationStatus>>> GetApplicationsByUser(long userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"user/{userId}/applications");

            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<LatestApplicationStatus>>.Failure($"Failed to fetch builder applications. {response.ReasonPhrase ?? ""}", response.StatusCode);

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var applications = await JsonSerializer.DeserializeAsync<IEnumerable<LatestApplicationStatus>>(responseStream, JsonOptions);
            return Result<IEnumerable<LatestApplicationStatus>>.Success(applications!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching builder applications for user ID {UserId}", userId);
            return Result<IEnumerable<LatestApplicationStatus>>.Failure($"An error occurred while fetching the builder applications: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<ConnectionModels.ApiDiscordConnectionWithUsers>> GetUsersConnectedToDiscordAccount(ulong discordId)
    {
        try
        {
            var response = await httpClient.GetAsync($"discord/snowflakes/{discordId}?includeUsers=true");
            
            if (!response.IsSuccessStatusCode)
                return Result<ConnectionModels.ApiDiscordConnectionWithUsers>.Failure($"Failed to fetch users. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var users = await JsonSerializer.DeserializeAsync<ConnectionModels.ApiDiscordConnectionWithUsers>(responseStream, JsonOptions);
            return Result<ConnectionModels.ApiDiscordConnectionWithUsers>.Success(users!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching users connected to Discord account with ID {DiscordId}", discordId);
            return Result<ConnectionModels.ApiDiscordConnectionWithUsers>.Failure($"An error occurred while fetching users: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<ConnectionModels.ApiDiscordAccount>>> GetDiscordAccountsForUser(long userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"user/{userId}/accounts/discord");

            if (!response.IsSuccessStatusCode)
                return Result<List<ConnectionModels.ApiDiscordAccount>>.Failure($"Failed to fetch Discord accounts. {response.ReasonPhrase ?? ""}", response.StatusCode);

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var discordAccounts = await JsonSerializer.DeserializeAsync<List<ConnectionModels.ApiDiscordAccount>>(responseStream, JsonOptions);
            return Result<List<ConnectionModels.ApiDiscordAccount>>.Success(discordAccounts!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching Discord accounts for user ID {UserId}", userId);
            return Result<List<ConnectionModels.ApiDiscordAccount>>.Failure($"An error occurred while fetching Discord accounts: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<string>> GetDiscordConnectUrl(long userId, string redirectUri)
    {
        try
        {
            var response = await httpClient.GetAsync($"discord/oauth/connection-link?redirectUrl={Uri.EscapeDataString(redirectUri)}&userId={userId}");
            
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Failed to fetch Discord connect URL for user ID {UserId}. Response: {ReasonPhrase}", userId, response.ReasonPhrase);
            
            return !response.IsSuccessStatusCode 
                ? Result<string>.Failure($"Failed to fetch Discord connect URL. {response.ReasonPhrase ?? ""}", response.StatusCode) 
                : Result<string>.Success(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching Discord connect URL for user ID {UserId}", userId);
            return Result<string>.Failure($"An error occurred while fetching Discord connect URL: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<string>> GetDiscordDisconnectUrl(long userId, long discordConnectionId, string redirectUri)
    {
        try 
        {
            var response = await httpClient.GetAsync($"discord/oauth/disconnect-link?redirectUrl={HttpUtility.UrlEncode(redirectUri)}&userId={userId}&discordConnectionId={discordConnectionId}");
            
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Failed to fetch Discord disconnect URL for user ID {UserId} and connection ID {DiscordConnectionId}. Response: {ReasonPhrase}", userId, discordConnectionId, response.ReasonPhrase);
            
            return !response.IsSuccessStatusCode 
                ? Result<string>.Failure($"Failed to fetch Discord disconnect URL. {response.ReasonPhrase ?? ""}", response.StatusCode) 
                : Result<string>.Success(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching Discord disconnect URL for user ID {UserId} and connection ID {DiscordConnectionId}", userId, discordConnectionId);
            return Result<string>.Failure($"An error occurred while fetching Discord disconnect URL: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<string>> GetPatreonConnectUrl(long userId, string redirectUri)
    {
        try 
        {
            var response = await httpClient.GetAsync($"patreon/oauth/connection-link?redirectUrl={Uri.EscapeDataString(redirectUri)}&userId={userId}");
            
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Failed to fetch Patreon connect URL for user ID {UserId}. Response: {ReasonPhrase}", userId, response.ReasonPhrase);
            
            return !response.IsSuccessStatusCode 
                ? Result<string>.Failure($"Failed to fetch Patreon connect URL. {response.ReasonPhrase ?? ""}", response.StatusCode) 
                : Result<string>.Success(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching Patreon connect URL for user ID {UserId}", userId);
            return Result<string>.Failure($"An error occurred while fetching Patreon connect URL: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<string>> GetPatreonDisconnectUrl(long userId, long patreonConnectionId, string redirectUri)
    {
        try
        {
            var response = await httpClient.GetAsync($"patreon/oauth/disconnect-link?redirectUrl={Uri.EscapeDataString(redirectUri)}&userId={userId}&patreonConnectionId={patreonConnectionId}");
            
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Failed to fetch Patreon disconnect URL for user ID {UserId} and connection ID {PatreonConnectionId}. Response: {ReasonPhrase}", userId, patreonConnectionId, response.ReasonPhrase);
            
            return !response.IsSuccessStatusCode 
                ? Result<string>.Failure($"Failed to fetch Patreon disconnect URL. {response.ReasonPhrase ?? ""}", response.StatusCode) 
                : Result<string>.Success(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching Patreon disconnect URL for user ID {UserId} and connection ID {PatreonConnectionId}", userId, patreonConnectionId);
            return Result<string>.Failure($"An error occurred while fetching Patreon disconnect URL: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<User>> GetUserByMinecraftUuid(Guid minecraftUuid)
    {
        try
        {
            var response = await httpClient.GetAsync($"user/{minecraftUuid}");
            
            if (!response.IsSuccessStatusCode)
                return Result<User>.Failure($"Failed to fetch user. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var user = await JsonSerializer.DeserializeAsync<User>(responseStream, JsonOptions);
            return Result<User>.Success(user!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching the user by Minecraft UUID {MinecraftUuid}", minecraftUuid);
            return Result<User>.Failure($"An error occurred while fetching the user: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<ConnectionModels.ApiPatreonConnectionWithUsers>> GetUsersConnectedToPatreonAccount(
        long patreonConnectionId)
    {
        try
        {
            var response = await httpClient.GetAsync($"user/{patreonConnectionId}/patreon");
            
            if (!response.IsSuccessStatusCode)
                return Result<ConnectionModels.ApiPatreonConnectionWithUsers>.Failure($"Failed to fetch patron accounts. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var patronAccounts = await JsonSerializer.DeserializeAsync<ConnectionModels.ApiPatreonConnectionWithUsers>(responseStream, JsonOptions);
            return Result<ConnectionModels.ApiPatreonConnectionWithUsers>.Success(patronAccounts!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching users connected to Patreon account with ID {PatreonConnectionId}", patreonConnectionId);
            return Result<ConnectionModels.ApiPatreonConnectionWithUsers>.Failure($"An error occurred while fetching patron accounts: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<List<ConnectionModels.ApiPatreonAccount>>> GetPatronAccountsForUser(long userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"user/{userId}/accounts/patreon");
      
            if (!response.IsSuccessStatusCode)
                return Result<List<ConnectionModels.ApiPatreonAccount>>.Failure($"Failed to fetch patron accounts. {response.ReasonPhrase ?? ""}", response.StatusCode);

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var patronAccounts = await JsonSerializer.DeserializeAsync<List<ConnectionModels.ApiPatreonAccount>>(responseStream, JsonOptions);
            return Result<List<ConnectionModels.ApiPatreonAccount>>.Success(patronAccounts!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching patron accounts for user ID {UserId}", userId);
            return Result<List<ConnectionModels.ApiPatreonAccount>>.Failure($"An error occurred while fetching patron accounts: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<User>> GetUserById(long userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"user/{userId}");
            
            if (!response.IsSuccessStatusCode)
                return Result<User>.Failure($"Failed to fetch user. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var user = await JsonSerializer.DeserializeAsync<User>(responseStream, JsonOptions);
            return Result<User>.Success(user!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching the user by ID {UserId}", userId);
            return Result<User>.Failure($"An error occurred while fetching the user: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IEnumerable<ResourcePackBranch>>> GetResourcePackBranches()
    {
        try
        {
            var response = await httpClient.GetAsync("resource/resourcepack/branches");
            
            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<ResourcePackBranch>>.Failure($"Failed to fetch resource pack branches. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var branches = await JsonSerializer.DeserializeAsync<IEnumerable<ResourcePackBranch>>(responseStream, JsonOptions);
            return Result<IEnumerable<ResourcePackBranch>>.Success(branches ?? []);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching resource pack branches.");
            return Result<IEnumerable<ResourcePackBranch>>.Failure($"An error occurred while fetching resource pack branches: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    /// <inheritdoc />
    public async Task<Result<ResourcePackDownloadRequest>> GetResourcePackDownloadLink(string branch)
    {
        try
        {
            var response = await httpClient.PostAsync($"resource/resourcepack/download-request?branch={Uri.EscapeDataString(branch)}", null);
            
            if (!response.IsSuccessStatusCode)
                return Result<ResourcePackDownloadRequest>.Failure($"Failed to generate resource pack download link. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var downloadRequest = await JsonSerializer.DeserializeAsync<ResourcePackDownloadRequest>(responseStream, JsonOptions);
            return Result<ResourcePackDownloadRequest>.Success(downloadRequest!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while generating a resource pack download link for branch {Branch}.", branch);
            return Result<ResourcePackDownloadRequest>.Failure($"An error occurred while generating the download link: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }
}