using NetCord;
using NetCord.Rest;

namespace GreenbotTwo.Embeds;

public class GenericEmbeds
{

    public static EmbedProperties Success(string title, string description)
    {
        return new EmbedProperties()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorHelpers.Success);
    }
    
    public static EmbedProperties Info(string title, string description)
    {
        return new EmbedProperties()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorHelpers.Info);
    }
    
    public static EmbedProperties Warning(string title, string description)
    {
        return new EmbedProperties()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorHelpers.Warning);
    }
    
    public static EmbedProperties UserError(string? title, string description) 
    {
        return new EmbedProperties()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorHelpers.Failure);
    }
    
    public static EmbedProperties InternalError(string title, string description) 
    {
        return new EmbedProperties()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorHelpers.Error);
    }
    
}