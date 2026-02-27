using NetCord;

namespace GreenbotTwo.Extensions;

public static class GuildUserExtensions
{
    public static string Mention(this GuildUser user)
    {
        return $"<@{user.Id}>";
    }

    public static string Mention(this User user)
    {
        return $"<@{user.Id}>";
    }

    public static string Mention(this ulong userId)
    {
        return $"<@{userId}>";
    }
    
}