using GreenbotTwo.Models;

namespace GreenbotTwo.Services.Interfaces;

public interface IMojangService
{
    
    Task<Result<MinecraftSlimProfile>> GetMinecraftProfileByUsername(string username);
    
}