using NetCord;
using NetCord.Rest;

namespace GreenbotTwo.Extensions;

public static class InteractionExtensions
{
    /// <summary>
    /// Sends a response to the interaction.
    /// </summary>
    /// <param name="interaction"></param>
    /// <param name="embeds"></param>
    /// <param name="flags"></param>
    /// <param name="components"></param>
    /// <returns></returns>
    public static Task<InteractionCallbackResponse?> SendResponse(this IInteraction interaction, EmbedProperties[]? embeds = null, MessageFlags? flags = null, IMessageComponentProperties[]? components = null)
    {
        var messageProperties = new InteractionMessageProperties().WithEmbeds(embeds).WithFlags(flags).WithComponents(components);
        return interaction.SendResponseAsync(InteractionCallback.Message(messageProperties));
    }
    
    /// <summary>
    /// Modifies the message the interaction was derived from.
    /// </summary>
    /// <param name="interaction"></param>
    /// <param name="embeds"></param>
    /// <param name="components"></param>
    /// <returns></returns>
    public static Task<InteractionCallbackResponse?> SendModifyResponse(this Interaction interaction, EmbedProperties[]? embeds = null, IMessageComponentProperties[]? components = null)
    {
        return interaction.SendResponseAsync(InteractionCallback.ModifyMessage(options =>
        {
            if (embeds is not null) options.WithEmbeds(embeds);
            if (components is not null) options.WithComponents(components);
        }));
    }

    /// <summary>
    /// Shows the user a loading state while the interaction defers the response to be sent later.
    /// </summary>
    /// <param name="interaction"></param>
    /// <param name="flags"></param>
    /// <returns></returns>
    public static Task<InteractionCallbackResponse?> SendNotifyLoadingResponse(this IInteraction interaction, MessageFlags? flags = null)
    {
        return interaction.SendResponseAsync(InteractionCallback.DeferredMessage(flags));
    }

    /// <summary>
    /// Does not show the user a loading state, tells discord that we will respond to the interaction later.
    /// </summary>
    /// <param name="interaction"></param>
    /// <returns></returns>
    public static Task<InteractionCallbackResponse?> SendModifyLaterResponse(this Interaction interaction)
    {
        return interaction.SendResponseAsync(InteractionCallback.DeferredModifyMessage);
    }
    
    /// <summary>
    /// Modifies the already sent interaction response.
    /// </summary>
    /// <param name="interaction"></param>
    /// <param name="embeds"></param>
    /// <param name="components"></param>
    /// <returns></returns>
    public static Task<RestMessage> ModifyResponse(this Interaction interaction, EmbedProperties[]? embeds = null, IMessageComponentProperties[]? components = null, MessageFlags? flags = null)
    {
        return interaction.ModifyResponseAsync(options =>
        {
            if (embeds is not null) options.WithEmbeds(embeds);
            if (components is not null) options.WithComponents(components);
            if (flags is not null) options.WithFlags(flags);
        });
    }
}