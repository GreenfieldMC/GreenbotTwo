using GreenbotTwo.Models;

namespace GreenbotTwo.Services.Interfaces;

public interface IMojangService
{
    
    Task<Result<MinecraftProfile>> GetMinecraftProfileByUsername(string username);
    
}