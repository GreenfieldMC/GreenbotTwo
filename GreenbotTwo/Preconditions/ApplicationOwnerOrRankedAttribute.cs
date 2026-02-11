using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Preconditions;

/// <summary>
/// Marks a command parameter (for a command relating to Applications) as requiring the user to be either the application owner or have a specific role.
/// </summary>
/// <param name="configurationOptionName">The configuration key that contains the required role id.</param>
public class ApplicationOwnerOrRankedAttribute<T>(string configurationOptionName) : ParameterPreconditionAttribute<ApplicationCommandContext> where T : class
{
    public override async ValueTask<PreconditionResult> EnsureCanExecuteAsync(object? value, ApplicationCommandContext context, IServiceProvider? serviceProvider)
    {
        if (value is null) 
            return PreconditionResult.Success;
        
        if (!long.TryParse(value.ToString(), out var appId)) 
            return PreconditionResult.Fail("Invalid application ID.");

        var gfApiService = serviceProvider?.GetService<IGreenfieldApiService>() ?? throw new InvalidOperationException("IGreenfieldApiService service is not available.");
        var appResult = await gfApiService.GetApplicationById(appId);
        if (!appResult.TryGetDataNonNull(out var application)) 
            return PreconditionResult.Fail("Application not found.");
        
        var userResult = await gfApiService.GetUsersConnectedToDiscordAccount(context.User.Id);
        if (!userResult.TryGetDataNonNull(out var discordConnection)) 
            return PreconditionResult.Fail("There are no users linked to your Discord account.");
        
        if (discordConnection.Users.Any(u => u.UserId == application.UserId))
            return PreconditionResult.Success;
        
        var commandSettings = serviceProvider?.GetService<IOptions<T>>() ?? throw new InvalidOperationException("IOptions<T> service is not available.");
        var property = commandSettings.Value.GetType().GetProperty(configurationOptionName) ?? throw new InvalidOperationException($"Configuration option '{configurationOptionName}' does not exist on type '{typeof(T).Name}'.");
        if (!ulong.TryParse(property.GetValue(commandSettings.Value)?.ToString(), out var roleId))
            throw new InvalidOperationException($"Configuration option '{configurationOptionName}' is not a valid ulong.");

        if (context.User is not GuildUser user)
            return PreconditionResult.Fail("User information is not available.");
        
        if (user.GetRoles(context.Guild).Any(r => r.Id == roleId)) 
            return PreconditionResult.Success;
        
        return PreconditionResult.Fail("User does not have the required role.");
    }
    
}