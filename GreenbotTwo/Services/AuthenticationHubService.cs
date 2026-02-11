using System.Text.Json;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GreenbotTwo.Services;

public class AuthenticationHubService(ILogger<IAuthenticationHubService> logger, HttpClient httpClient) : IAuthenticationHubService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    
    public async Task<Result> ValidateUsername(string minecraftUsername)
    {
        try
        {
            var response = await httpClient.PostAsync("validate", 
                new StringContent(JsonSerializer.Serialize(new { username = minecraftUsername }, JsonOptions), System.Text.Encoding.UTF8, "application/json"));
            
            if (!response.IsSuccessStatusCode)
                return Result<string>.Failure("Failed to validate username.", response.StatusCode);
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<(string Message, int Status)>(content, JsonOptions);
            return Result<string>.Success(result.Message);
        } catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while validating username {MinecraftUsername}", minecraftUsername);
            return Result<string>.Failure($"An error occurred while validating username: {ex.Message}");
        }
    }

    public async Task<Result<string>> Authorize(string minecraftUsername, string authCode)
    {
        try
        {
            var response = await httpClient.PostAsync("authorize", 
                new StringContent(JsonSerializer.Serialize(new { username = minecraftUsername, authCode = authCode }, JsonOptions), System.Text.Encoding.UTF8, "application/json"));
            
            if (!response.IsSuccessStatusCode)
                return Result<string>.Failure("Failed to authorize user.", response.StatusCode);
            
            var content = response.Content.ReadAsStringAsync().Result;
            var result = JsonSerializer.Deserialize<(string Message, int Status)>(content, JsonOptions);
            return Result<string>.Success(result.Message);
        } catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while authorizing user {MinecraftUsername}", minecraftUsername);
            return Result<string>.Failure($"An error occurred while authorizing user: {ex.Message}");
        }
    }

    public async Task<Result> RemoveAuthSession(Guid minecraftUuid)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"reset?uuid={minecraftUuid}");
            
            return !response.IsSuccessStatusCode 
                ? Result.Failure("Failed to remove auth session.", response.StatusCode) 
                : Result.Success();
        } catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while removing auth session for UUID {MinecraftUuid}", minecraftUuid);
            return Result.Failure($"An error occurred while removing auth session: {ex.Message}");
        }
    }
}