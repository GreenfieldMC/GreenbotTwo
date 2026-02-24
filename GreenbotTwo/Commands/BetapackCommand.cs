using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.NetCordSupport.AutocompleteProviders;
using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class BetapackCommand(IOptions<BetapackCommandSettings> settings, IGreenfieldApiService greenfieldApiService, ILogger<IApplicationCommandContext> logger) : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly BetapackCommandSettings _settings = settings.Value;

    [SlashCommand("betapack", "Download the beta version of the Greenfield Resource Pack.")]
    public async Task Betapack(
        [SlashCommandParameter(Name = "branch", Description = "The branch to download. Defaults to the configured default branch.", AutocompleteProviderType = typeof(BranchAutocompleteProvider))] string? branch = null)
    {
        logger.LogCommandExecution(Context, branch is null ? null : $"branch: {branch}");
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        var selectedBranch = branch ?? _settings.DefaultBranch;

        var downloadResult = await greenfieldApiService.GetResourcePackDownloadLink(selectedBranch);
        if (!downloadResult.TryGetDataNonNull(out var download))
        {
            logger.LogCommandError(Context, $"Failed to get download link for branch '{selectedBranch}'. Status code: {downloadResult.StatusCode}, Error: {downloadResult.ErrorMessage}");
            await Context.Interaction.ModifyResponseAsync(options =>
                options.WithEmbeds([GenericEmbeds.InternalError("Download Failed", $"Failed to generate a download link for the `{selectedBranch}` branch.")]));
            return;
        }

        await Context.Interaction.ModifyResponseAsync(options =>
            options
                .WithContent($"Your one-time download link for the `{selectedBranch}` branch is ready; it will expire <t:{DateTimeOffset.UtcNow.AddMinutes(download.ExpiresInMinutes).ToUnixTimeSeconds()}:R> if unused!")
                .WithComponents([
                    new ActionRowProperties([
                        new LinkButtonProperties(download.DownloadUrl, "Download")
                    ])
                ]));
    }
}