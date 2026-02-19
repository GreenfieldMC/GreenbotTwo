using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.NetCordSupport.Preconditions;

/// <summary>
/// Make a user parameter require the sender to have a specific permission, or if the user parameter is the same as the sender.
/// </summary>
/// <param name="configurationOptionKey"></param>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TUserType"></typeparam>
public class UserRequiresAnyRoleOrSelfAttribute<T, TUserType>(string configurationOptionKey) : ParameterPreconditionAttribute<ApplicationCommandContext> where T : class
{
    public override async ValueTask<PreconditionResult> EnsureCanExecuteAsync(object? value, ApplicationCommandContext context, IServiceProvider? serviceProvider)
    {
        var commandSettings = serviceProvider?.GetService<IOptions<T>>() ?? throw new InvalidOperationException(nameof(IOptions<T>) + " service not available.");
        var property = commandSettings.Value.GetType().GetProperty(configurationOptionKey) ?? throw new InvalidOperationException($"Configuration option '{configurationOptionKey}' does not exist on type '{typeof(T).Name}'.");
        var allowedRoleIds = property.GetValue(commandSettings.Value) as IEnumerable<ulong> ?? throw new InvalidOperationException($"Configuration option '{configurationOptionKey}' is not a valid list of ulong role IDs.");
        
        //user being checked is a minecraft user
        if (typeof(TUserType) == typeof(string))
        {
            var minecraftUsername = value as string ?? throw new InvalidOperationException($"Could not resolve username from {value}");
            var gfApService = serviceProvider?.GetService<IGreenfieldApiService>();
            if (!(await gfApService!.GetUsersConnectedToDiscordAccount(context.User.Id)).TryGetDataNonNull(out var discordConnection))
                return PreconditionResult.Fail("There are no connections to a user with your Discord account.");

            //a user connected to this discord account matches the requested username.
            if (discordConnection.Users.Exists(u => u.Username.Equals(minecraftUsername, StringComparison.OrdinalIgnoreCase)))
                return PreconditionResult.Success;
        }

        //user being checked is a discord user
        if (typeof(TUserType) == typeof(User))
        {
            var user = value as User ?? context.User;
            if (user.Id == context.User.Id)
                return PreconditionResult.Success;
        }
        
        if (context.User is not GuildUser guildUser)
            return PreconditionResult.Fail("User information is not available.");
            
        if (guildUser.GetRoles(context.Guild!).Any(r => allowedRoleIds.Contains(r.Id)))
            return PreconditionResult.Success;
        
        return PreconditionResult.Fail("You do not have sufficient permissions.");
    }
    
}