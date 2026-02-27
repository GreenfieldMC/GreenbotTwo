using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using Application = GreenbotTwo.Models.GreenfieldApi.Application;

namespace GreenbotTwo.NetCordSupport.Preconditions;

/// <summary>
/// Marks a command parameter (for a command relating to Applications) as requiring the user to be either the application owner or have a specific role.
/// </summary>
/// <param name="configurationOptionName">The configuration key that contains the required role id.</param>
public class ApplicationRequiresAnyRoleOrOwnerAttribute<T>(string configurationOptionName) : ParameterPreconditionAttribute<ApplicationCommandContext> where T : class
{
    public override async ValueTask<PreconditionResult> EnsureCanExecuteAsync(object? value, ApplicationCommandContext context, IServiceProvider? serviceProvider)
    {
        if (value is null) 
            return PreconditionResult.Success;

        var gfApiService = serviceProvider?.GetService<IGreenfieldApiService>() ?? throw new InvalidOperationException("IGreenfieldApiService service is not available.");
        var application = value as Application ?? throw new InvalidOperationException("Application parameter was not parsed.");
        
        var userResult = await gfApiService.GetUsersConnectedToDiscordAccount(context.User.Id);
        if (!userResult.TryGetDataNonNull(out var discordConnection)) 
            return PreconditionResult.Fail("There are no users linked to your Discord account.");
        
        if (discordConnection.Users.Any(u => u.UserId == application.UserId))
            return PreconditionResult.Success;
        
        var commandSettings = serviceProvider?.GetService<IOptions<T>>() ?? throw new InvalidOperationException("IOptions<T> service is not available.");
        var property = commandSettings.Value.GetType().GetProperty(configurationOptionName) ?? throw new InvalidOperationException($"Configuration option '{configurationOptionName}' does not exist on type '{typeof(T).Name}'.");
        var allowedRoleIds = property.GetValue(commandSettings.Value) as IEnumerable<ulong> ?? throw new InvalidOperationException($"Configuration option '{configurationOptionName}' is not a valid list of ulong role IDs.");

        if (context.User is not GuildUser user)
            return PreconditionResult.Fail("User information is not available.");
        
        if (user.GetRoles(context.Guild).Any(r => allowedRoleIds.Contains(r.Id)))
            return PreconditionResult.Success;
        
        return PreconditionResult.Fail("You do not have sufficient permissions.");
    }
    
}