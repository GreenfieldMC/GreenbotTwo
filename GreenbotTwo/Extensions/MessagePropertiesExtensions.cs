using NetCord.Rest;

namespace GreenbotTwo.Extensions;

public static class MessagePropertiesExtensions
{
    public static InteractionMessageProperties ToInteractionMessageProperties(this MessageProperties messageProperties)
    {
        return new InteractionMessageProperties
        {
            Content = messageProperties.Content,
            Embeds = messageProperties.Embeds,
            AllowedMentions = messageProperties.AllowedMentions,
            Components = messageProperties.Components,
            Attachments = messageProperties.Attachments,
            Flags = messageProperties.Flags,
            Tts = messageProperties.Tts,
            Poll = messageProperties.Poll,
        };
    }
}