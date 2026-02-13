using GreenbotTwo.Embeds;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Preconditions;

public class CommandPreconditionResponseHandler : IApplicationCommandResultHandler<ApplicationCommandContext>
{
    public ValueTask HandleResultAsync(IExecutionResult result, ApplicationCommandContext context, GatewayClient? client,
        ILogger logger, IServiceProvider services)
    {
        if (result is not IFailResult failResult)
            return default;

        var resultMessage = failResult.Message;

        var interaction = context.Interaction;

        if (failResult is IExceptionResult exceptionResult)
            logger.LogError(exceptionResult.Exception, "Execution of an application command of name '{Name}' failed with an exception", interaction.Data.Name);
        else
            logger.LogDebug("Execution of an application command of name '{Name}' failed with '{Message}'", interaction.Data.Name, resultMessage);

        InteractionMessageProperties message = new()
        {
            Flags = MessageFlags.Ephemeral,
            Embeds = [GenericEmbeds.UserError(null, resultMessage)]
        };

        return new ValueTask(interaction.SendResponseAsync(InteractionCallback.Message(message)));
    }
}