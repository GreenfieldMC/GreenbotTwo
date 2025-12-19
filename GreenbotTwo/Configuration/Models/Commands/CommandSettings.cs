namespace GreenbotTwo.Configuration.Models.Commands;

public class CommandSettings
{
    public required InstallCommandSettings InstallCommand { get; init; }
    public required BetapackCommandSettings BetapackCommand { get; init; }
}