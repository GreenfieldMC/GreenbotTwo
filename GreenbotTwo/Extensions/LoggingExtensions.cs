using Microsoft.Extensions.Logging;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GreenbotTwo.Extensions;

public static class LoggingExtensions
{

    #region Command Logging

    /// <param name="logger"></param>
    extension(ILogger<IApplicationCommandContext> logger)
    {
        /// <summary>
        /// Log a command execution
        /// </summary>
        /// <param name="context"></param>
        /// <param name="commandArguments"></param>
        public void LogCommandExecution(IApplicationCommandContext context, string? commandArguments = null)
        {
            if (!logger.IsEnabled(LogLevel.Information)) return;
            if (commandArguments is null) 
                logger.LogInformation("{InteractionId} | User {User} executed: /{CommandName}", context.Interaction.Id, context.Interaction.User.Username, context.Interaction.Data.Name);
            else logger.LogInformation("{InteractionId} | User {User} executed: /{CommandName} {CommandArguments}", context.Interaction.Id, context.Interaction.User.Username, context.Interaction.Data.Name, commandArguments);
        }
    }

    #endregion
    
    #region Interaction Logging

    extension<T>(ILogger<T> logger) where T : IInteractionContext
    {

        /// <summary>
        /// Log information related to a component interaction, including the interaction ID for correlation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public void LogInteractionInfo(T context, string message)
        {
            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("{InteractionId} | {Message}", context.Interaction.Id, message);
        }
        
        /// <summary>
        /// Log debug information related to a component interaction, including the interaction ID for correlation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public void LogInteractionDebug(T context, string message)
        {
            if (logger.IsEnabled(LogLevel.Debug)) logger.LogDebug("{InteractionId} | {Message}", context.Interaction.Id,  message);
        }
        
        /// <summary>
        /// Log error information related to a component interaction, including the interaction ID for correlation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public void LogInteractionError(T context, string message)
        {
            logger.LogError("{InteractionId} | {Message}", context.Interaction.Id, message);
        }
        
        /// <summary>
        /// Log warning information related to a component interaction, including the interaction ID for correlation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        public void LogInteractionWarning(T context, string message)
        {
            logger.LogWarning("{InteractionId} | {Message}", context.Interaction.Id, message);
        }
        
    }
    
    #endregion
    
}