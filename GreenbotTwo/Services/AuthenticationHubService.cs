using System.Text.Json;
using GreenbotTwo.Models;
using GreenbotTwo.Models.AuthHub;
using GreenbotTwo.Services.Interfaces;

namespace GreenbotTwo.Services;

public class AuthenticationHubService(HttpClient httpClient) : IAuthenticationHubService
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
            return Result<string>.Failure($"An error occurred while authorizing user: {ex.Message}");
        }
    }

    public Task<Result<AuthHubAppConnections>> GetApplications(string applicationCallUrl)
    {
        throw new NotImplementedException();
    }
}