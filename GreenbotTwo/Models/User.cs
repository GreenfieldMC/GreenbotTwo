namespace GreenbotTwo.Models;

public record User(long UserId, Guid MinecraftUuid, string Username, DateTime CreatedOn);