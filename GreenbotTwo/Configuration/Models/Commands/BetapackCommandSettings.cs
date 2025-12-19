namespace GreenbotTwo.Configuration.Models.Commands;

public class BetapackCommandSettings
{
    public required BetapackEmbed OneTimeDownload { get; init; }
    public required BetapackEmbed GitBasedDownload { get; init; }
    public required BetapackEmbed UpdatingThePack { get; init; }
}