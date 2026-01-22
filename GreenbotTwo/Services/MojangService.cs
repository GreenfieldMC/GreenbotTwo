using System.Net;
using System.Text.Json;
using GreenbotTwo.Models;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GreenbotTwo.Services;

public class MojangService : IMojangService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MojangService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MojangService(ILogger<MojangService> logger, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _logger = logger;
        
    }
    
    public async Task<Result<MinecraftProfile>> GetMinecraftProfileByUsername(string username)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/users/profiles/minecraft/{username}");
            if (!response.IsSuccessStatusCode) 
                return Result<MinecraftProfile>.Failure($"Failed to fetch Minecraft profile for username '{username}'. Status code: {response.StatusCode}");
            
            var contentStream = await response.Content.ReadAsStreamAsync();
            var profile = await JsonSerializer.DeserializeAsync<MinecraftProfile>(contentStream, JsonOptions);
            
            return profile == null 
                ? throw new Exception("Profile could not be deserialized from response.") 
                : Result<MinecraftProfile>.Success(profile);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while fetching the Minecraft profile for username '{Username}'", username);
            return Result<MinecraftProfile>.Failure($"An error occurred while fetching the Minecraft profile for username '{username}': {e.Message}", HttpStatusCode.InternalServerError);
        }
    }
}