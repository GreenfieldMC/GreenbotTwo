namespace GreenbotTwo.Models.GreenfieldApi;

public record User(long UserId, Guid MinecraftUuid, string Username, DateTime CreatedOn);