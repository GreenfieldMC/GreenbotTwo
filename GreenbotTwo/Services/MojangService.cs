using System.Net;
using System.Text.Json;
using GreenbotTwo.Models;
using GreenbotTwo.Services.Interfaces;

namespace GreenbotTwo.Services;

public class MojangService : IMojangService
{

    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    
    public MojangService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
    }
    
    public async Task<Result<MinecraftSlimProfile>> GetMinecraftProfileByUsername(string username)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/users/profiles/minecraft/{username}");
            if (!response.IsSuccessStatusCode) 
                return Result<MinecraftSlimProfile>.Failure($"Failed to fetch Minecraft profile for username '{username}'. Status code: {response.StatusCode}");
            
            var contentStream = await response.Content.ReadAsStreamAsync();
            var profile = await JsonSerializer.DeserializeAsync<MinecraftSlimProfile>(contentStream, JsonOptions);
            
            return profile == null 
                ? throw new Exception("Profile could not be deserialized from response.") 
                : Result<MinecraftSlimProfile>.Success(profile);
        }
        catch (Exception e)
        {
            return Result<MinecraftSlimProfile>.Failure($"An error occurred while fetching the Minecraft profile for username '{username}': {e.Message}", HttpStatusCode.InternalServerError);
        }
    }
}