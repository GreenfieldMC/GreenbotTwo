using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Services;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands;

public class CodesCommand(IGreenfieldApiService greenfieldApiService) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("codes", "Get all build codes from Greenfield")]
    public async Task Codes(
        [SlashCommandParameter(Name = "run_for", Description = "The discord user who needs the install instructions.")] User? runFor = null)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        
        var isSameUser = runFor is null || runFor.Id == Context.User.Id;
        
        if ((runFor ?? Context.User) is not GuildUser userWhoNeedsCodes)
        {
            await Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([GenericEmbeds.UserError("Invalid User", "The specified user is not a member of this server.")])
                .WithFlags(MessageFlags.Ephemeral));
            return;
        }

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        
        var buildCodeResult = await greenfieldApiService.GetBuildCodes();   
        
        if (!buildCodeResult.TryGetDataNonNull(out var buildCodesEnum))
        {
            await Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([GenericEmbeds.InternalError("Internal Application Error",$"An error occurred while fetching build codes: {buildCodeResult.ErrorMessage}")])
                .WithFlags(MessageFlags.Ephemeral));
            return;
        }

        var buildCodes = buildCodesEnum.ToList();

        if (buildCodes.Count == 0)
        {
            _ = Context.Interaction.ModifyResponseAsync(options => options
                .WithEmbeds([GenericEmbeds.UserError("No Build Codes Found", "There are currently no build codes available.")])
                .WithFlags(MessageFlags.Ephemeral));
            return;
        }

        var message = new MessageProperties()
            .WithContent(userWhoNeedsCodes.Mention())
            .WithAllowedMentions(new AllowedMentionsProperties().AddAllowedUsers(userWhoNeedsCodes.Id))
            .WithEmbeds([
                new EmbedProperties()
                    .WithTitle("Current Server Build Codes")
                    .WithFields(buildCodes
                        .OrderBy(c => c.ListOrder)
                        .Select((code, idx) => new EmbedFieldProperties().WithInline(false).WithName(" ")
                            .WithValue($"**__{idx + 1}.__)** {code.Code}"))
                    )
            ])
            .WithFlags(MessageFlags.Ephemeral);
        
        if (isSameUser)
        {
            _ = Context.Interaction.ModifyResponseAsync(_ => message.ToInteractionMessageProperties());
            return;
        }

        await Context.Interaction.DeleteResponseAsync();
        _ = Context.Channel.SendMessageAsync(message);
    }
}