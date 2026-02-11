using GreenbotTwo.Configuration.Models.Commands;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class BetapackCommand(IOptions<BetapackCommandSettings> settings) : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly BetapackCommandSettings _settings = settings.Value;
    
    [SlashCommand("betapack", "Get instructions to install the beta version of the Greenfield Resource Pack.")]
    public async Task Betapack(
        [SlashCommandParameter(Name = "run_for", Description = "The discord user who needs the install instructions.")] User? runFor = null)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        
        var userWhoNeedsInstructions = runFor ?? Context.User;

        var oneTimeDownloadConfig = _settings.OneTimeDownload;
        var oneTimeDownloadMessage = new MessageProperties()
            .WithContent($"{userWhoNeedsInstructions.Mention()}, {oneTimeDownloadConfig.MessageContent}")
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsInstructions.Id))
            .WithEmbeds([
                new EmbedProperties()
                    .WithTitle(oneTimeDownloadConfig.Title)
                    .WithFields(oneTimeDownloadConfig.GetEmbedFields())
                    .WithColor(ColorHelpers.Info)
            ]);
        
        var gitBasedDownloadConfig = _settings.GitBasedDownload;
        var gitBasedDownloadMessage = new MessageProperties()
            .WithContent($"{gitBasedDownloadConfig.MessageContent}")
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsInstructions.Id))
            .WithEmbeds([
                new EmbedProperties()
                    .WithTitle(gitBasedDownloadConfig.Title)
                    .WithFields(gitBasedDownloadConfig.GetEmbedFields())
                    .WithColor(ColorHelpers.Info)
            ]);
        
        var updatingThePackConfig = _settings.UpdatingThePack;
        var updatingThePackMessage = new MessageProperties()
            .WithContent($"{updatingThePackConfig.MessageContent}")
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsInstructions.Id))
            .WithEmbeds([
                new EmbedProperties()
                    .WithTitle(updatingThePackConfig.Title)
                    .WithFields(updatingThePackConfig.GetEmbedFields())
                    .WithColor(ColorHelpers.Info)
            ]);

        if (userWhoNeedsInstructions.Id == Context.User.Id)
        {
            await Context.Interaction.ModifyResponseAsync(options =>
                options
                    .WithContent(oneTimeDownloadMessage.Content)
                    .WithEmbeds(oneTimeDownloadMessage.Embeds)
                    .WithAllowedMentions(oneTimeDownloadMessage.AllowedMentions)
                );
            await Context.Interaction.SendFollowupMessageAsync(gitBasedDownloadMessage.ToInteractionMessageProperties().WithFlags(MessageFlags.Ephemeral));
            _ = Context.Interaction.SendFollowupMessageAsync(updatingThePackMessage.ToInteractionMessageProperties().WithFlags(MessageFlags.Ephemeral));
            return;
        }

        var msg = await Context.Channel.SendMessageAsync(oneTimeDownloadMessage);
        msg = await Context.Channel.SendMessageAsync(gitBasedDownloadMessage.WithMessageReference(MessageReferenceProperties.Reply(msg.Id)));
        _ = Context.Channel.SendMessageAsync(updatingThePackMessage.WithMessageReference(MessageReferenceProperties.Reply(msg.Id)));

    }
}