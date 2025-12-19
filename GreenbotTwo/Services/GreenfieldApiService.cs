using System.Net;
using System.Text.Json;
using System.Web;
using GreenbotTwo.Models;
using GreenbotTwo.Services.Interfaces;

namespace GreenbotTwo.Services;

public class GreenfieldApiService : IGreenfieldApiService
{

    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    
    public GreenfieldApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<Result<IEnumerable<BuildCode>>> GetBuildCodes()
    {
        try
        {
            var response = await _httpClient.GetAsync("buildcode/all");
            if (!response.IsSuccessStatusCode)
            {
                return Result<IEnumerable<BuildCode>>.Failure($"Failed to fetch build codes. {response.ReasonPhrase ?? ""}", response.StatusCode);
            }
        
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var responseData = JsonSerializer.Deserialize<IEnumerable<BuildCode>>(responseStream, JsonOptions);
            return Result<IEnumerable<BuildCode>>.Success(responseData ?? []);   
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<BuildCode>>.Failure($"An error occurred while fetching build codes: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<long>> SubmitBuilderApplication(BuilderApplicationForm applicationForm)
    {
        try
        {
            var submissionModel = new
            {
                DiscordId = applicationForm.DiscordId,
                MinecraftUsername = applicationForm.MinecraftProfile?.Name ??
                                    throw new InvalidOperationException("Minecraft profile is required."),
                MinecraftUuid = applicationForm.MinecraftProfile?.Uuid ??
                                throw new InvalidOperationException("Minecraft profile is required."),
                Age = applicationForm.Age,
                Nationality = applicationForm.Nationality,
                HouseBuildLinks = applicationForm.HouseBuildLinks,
                OtherBuildLinks = applicationForm.OtherBuildLinks,
                AdditionalBuildingInformation = applicationForm.AdditionalBuildingInformation,
                WhyJoinGreenfield = applicationForm.WhyJoinGreenfield,
                AdditionalComments = applicationForm.AdditionalComments
            };
            
            var content = new StringContent(JsonSerializer.Serialize(submissionModel, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("builderapplication/submit", content);
            
            if (!response.IsSuccessStatusCode)
                return Result<long>.Failure($"Failed to submit builder application. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var applicationId = await JsonSerializer.DeserializeAsync<long>(responseStream, JsonOptions);
            return Result<long>.Success(applicationId);
        }
        catch (Exception ex)
        {
            return Result<long>.Failure($"An error occurred while submitting the builder application: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<bool>> AddApplicationStatus(long applicationId, string status, string? statusMessage)
    {
        try 
        {
            var statusModel = new
            {
                Status = status,
                StatusMessage = statusMessage
            };
            
            var content = new StringContent(JsonSerializer.Serialize(statusModel, JsonOptions), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"builderapplication/application/{applicationId}/status/add", content);
            
            return !response.IsSuccessStatusCode ? Result<bool>.Failure($"Failed to add application status. {response.ReasonPhrase ?? ""}", response.StatusCode) : Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"An error occurred while adding the application status: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<BuilderApplication>> GetBuilderApplicationById(long applicationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"builderapplication/application/{applicationId}");

            if (!response.IsSuccessStatusCode)
                return Result<BuilderApplication>.Failure(
                    $"Failed to fetch builder application. {response.ReasonPhrase ?? ""}", response.StatusCode);

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var application = await JsonSerializer.DeserializeAsync<BuilderApplication>(responseStream, JsonOptions);
            return Result<BuilderApplication>.Success(application!);
        }
        catch (Exception ex)
        {
            return Result<BuilderApplication>.Failure($"An error occurred while fetching the builder application: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<IEnumerable<LatestBuildAppStatus>>> GetBuilderApplicationsByUser(long userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"builderapplication/applications/{userId}");

            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<LatestBuildAppStatus>>.Failure($"Failed to fetch builder applications. {response.ReasonPhrase ?? ""}", response.StatusCode);

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var applications = await JsonSerializer.DeserializeAsync<IEnumerable<LatestBuildAppStatus>>(responseStream, JsonOptions);
            return Result<IEnumerable<LatestBuildAppStatus>>.Success(applications!);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<LatestBuildAppStatus>>.Failure($"An error occurred while fetching the builder applications: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<IEnumerable<User>>> GetUsersByDiscordSnowflake(ulong discordId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"user/discord/{discordId}/users");
            
            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<User>>.Failure($"Failed to fetch users. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var users = await JsonSerializer.DeserializeAsync<IEnumerable<User>>(responseStream, JsonOptions);
            return Result<IEnumerable<User>>.Success(users!);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<User>>.Failure($"An error occurred while fetching users: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<IEnumerable<ulong>>> GetDiscordSnowflakesByUserId(long userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"user/{userId}/discord");
            
            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<ulong>>.Failure($"Failed to fetch linked Discord accounts. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var discordIds = await JsonSerializer.DeserializeAsync<IEnumerable<ulong>>(responseStream, JsonOptions);
            return Result<IEnumerable<ulong>>.Success(discordIds!);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<ulong>>.Failure($"An error occurred while fetching linked Discord accounts: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }
    
    public async Task<Result<IEnumerable<ulong>>> GetDiscordSnowflakesByMinecraftGuid(Guid minecraftUuid)
    {
        try
        {
            var response = await _httpClient.GetAsync($"user/{minecraftUuid}/discord");
            
            if (!response.IsSuccessStatusCode)
                return Result<IEnumerable<ulong>>.Failure($"Failed to fetch linked Discord accounts. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var discordIds = await JsonSerializer.DeserializeAsync<IEnumerable<ulong>>(responseStream, JsonOptions);
            return Result<IEnumerable<ulong>>.Success(discordIds!);
        }
        catch (Exception ex)
        {
            return Result<IEnumerable<ulong>>.Failure($"An error occurred while fetching linked Discord accounts: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<Result<User>> GetUserById(long userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"user/{userId}/userinfo");
            
            if (!response.IsSuccessStatusCode)
                return Result<User>.Failure($"Failed to fetch user. {response.ReasonPhrase ?? ""}", response.StatusCode);
            
            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var user = await JsonSerializer.DeserializeAsync<User>(responseStream, JsonOptions);
            return Result<User>.Success(user!);
        }
        catch (Exception ex)
        {
            return Result<User>.Failure($"An error occurred while fetching the user: {ex.Message}", HttpStatusCode.InternalServerError);
        }
    }
}