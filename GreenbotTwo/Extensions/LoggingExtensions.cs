using Microsoft.Extensions.Logging;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Extensions;

public static class LoggingExtensions
{

    #region Command Logging
    
    /// <summary>
    /// Log a command execution
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="context"></param>
    /// <param name="commandArguments"></param>
    public static void LogCommandExecution(this ILogger<IApplicationCommandContext> logger, IApplicationCommandContext context, string? commandArguments = null)
    {
        if (commandArguments is null) 
            logger.LogInformation("{InteractionId} | User {User} executed: /{CommandName}", context.Interaction.Id, context.Interaction.User.Username, context.Interaction.Data.Name);
        else logger.LogInformation("{InteractionId} | User {User} executed: /{CommandName} {CommandArguments}", context.Interaction.Id, context.Interaction.User.Username, context.Interaction.Data.Name, commandArguments);
    }
    
    public static void LogCommandInfo(this ILogger<IApplicationCommandContext> logger, IApplicationCommandContext context, string message)
    {
        logger.LogInformation("{InteractionId} | {Message}", context.Interaction.Id, message);
    }
    
    public static void LogCommandDebug(this ILogger<IApplicationCommandContext> logger, IApplicationCommandContext context, string message)
    {
        logger.LogDebug("{InteractionId} | {Message}", context.Interaction.Id,  message);
    }
    
    public static void LogCommandError(this ILogger<IApplicationCommandContext> logger, IApplicationCommandContext context, string message)
    {
        logger.LogError("{InteractionId} | {Message}", context.Interaction.Id, message);
    }
    
    public static void LogCommandWarning(this ILogger<IApplicationCommandContext> logger, IApplicationCommandContext context, string message)
    {
        logger.LogWarning("{InteractionId} | {Message}", context.Interaction.Id, message);
    }
    
    #endregion
    
}