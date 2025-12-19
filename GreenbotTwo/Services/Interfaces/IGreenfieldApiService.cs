using GreenbotTwo.Models;

namespace GreenbotTwo.Services.Interfaces;

public interface IGreenfieldApiService
{
    Task<Result<IEnumerable<BuildCode>>> GetBuildCodes();
    
    Task<Result<long>> SubmitBuilderApplication(BuilderApplicationForm applicationForm);
    
    Task<Result<bool>> AddApplicationStatus(long applicationId, string status, string? statusMessage);
    
    Task<Result<BuilderApplication>> GetBuilderApplicationById(long applicationId);
    
    Task<Result<IEnumerable<LatestBuildAppStatus>>> GetBuilderApplicationsByUser(long userId);
    
    Task<Result<IEnumerable<User>>> GetUsersByDiscordSnowflake(ulong discordId);
    
    Task<Result<IEnumerable<ulong>>> GetDiscordSnowflakesByUserId(long userId);
    
    Task<Result<User>> GetUserById(long userId);

    Task<Result<IEnumerable<ulong>>> GetDiscordSnowflakesByMinecraftGuid(Guid minecraftUuid);

}