using NetCord.Rest;

namespace GreenbotTwo.Configuration.Models.Commands;

public class BetapackEmbed
{
    public required string Title { get; set; }
    public required string MessageContent { get; set; }
    public required IEnumerable<string> Steps { get; set; }

    public IEnumerable<EmbedFieldProperties> GetEmbedFields()
    {
        return Steps.Select((s, idx) => new EmbedFieldProperties().WithName(" ").WithValue($"**__{idx + 1}.__)** {s}"));
    }
    
}