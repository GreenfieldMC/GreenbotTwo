using System.Text;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.Commands.ApplicationCommands;

public class AppStatusCommand(IApplicationService applicationService, IGreenfieldApiService gfApiService) : ApplicationCommandModule<ApplicationCommandContext>
{

    private static readonly EmbedProperties ErrorNoUsersLinkedToCurrentDiscordAccount =
        GenericEmbeds.UserError("Greenfield Application Service",
            "You do not have any users linked to your Discord account.");
    private static readonly EmbedProperties ErrorNoActiveApplications =
        GenericEmbeds.UserError("Greenfield Application Service",
            "You do not have any active applications at this time.");
    private static readonly EmbedProperties ErrorApplicationNotFound = GenericEmbeds.UserError("Greenfield Application Service", 
        "The application with the provided ID was not found. Please double-check the ID and try again.");
    
    [SlashCommand("appstatus", "View the status of your build application(s).")]
    public async Task AppStatus(long? applicationId = null)
    {
        await Context.Interaction.SendNotifyLoadingResponse(MessageFlags.Ephemeral);
        
        EmbedProperties? embed = null;

        //if no application id is provided, fetch all applications for all users linked to the current discord account
        if (applicationId is null)
        {
            var usersConnectedToCurrentAccountResponse = await gfApiService.GetUsersConnectedToDiscordAccount(Context.User.Id);
            if (!usersConnectedToCurrentAccountResponse.TryGetDataNonNull(out var connectionWithUsers))
            {
                await Context.Interaction.ModifyResponse(embeds: [ErrorNoUsersLinkedToCurrentDiscordAccount]);
                return;
            }

            var stringBuilder = new StringBuilder();
            
            foreach (var user in connectionWithUsers.Users)
            {
                var appsForUserResult = await gfApiService.GetApplicationsByUser(user.UserId);
                if (!appsForUserResult.TryGetDataNonNull(out var applications))
                    continue;
                
                var appList = applications.ToList();
                if (appList.Count == 0)
                    continue;

                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"__**Applications for user:**__ `{user.Username}`");

                var i = 0;
                foreach (var application in appList)
                {
                    if (i % 2 == 0) // 2 applications per line
                        stringBuilder.AppendLine();
                    stringBuilder.Append($"**ID:** `{application.ApplicationId}` ~~--~~ **Status:** `{application.LatestStatus?.Status}`\t\t");
                    i++;
                }
                
                embed = GenericEmbeds.Info("Greenfield Application Service",
                    stringBuilder.AppendLine().AppendLine("Here are your active applications. Use the `/viewapp <applicationId>` command to view more details about a specific application.").ToString().Trim());
            }

            if (embed is null)
            {
                await Context.Interaction.ModifyResponse([ErrorNoActiveApplications]);
                return;
            }

            await Context.Interaction.ModifyResponse(embeds: [embed]);
            return;

        }
        
        var foundAppResponse = await gfApiService.GetApplicationById(applicationId.Value);
        if (!foundAppResponse.TryGetDataNonNull(out var applicationDetails))
        {
            await Context.Interaction.ModifyResponse(embeds: [ErrorApplicationNotFound]);
            return;
        }
        
        embed = GenericEmbeds.Info("Greenfield Application Service",
            $"**Application ID:** `{applicationDetails.ApplicationId}`\n" +
            $"**Applicant:** `{applicationDetails.UserId}`\n" +
            $"**Current Status:** `{applicationDetails.BuildAppStatuses.OrderByDescending(s => s.CreatedOn).First().Status}`\n" +
            $"**Submitted On:** <t:{new DateTimeOffset(applicationDetails.CreatedOn).ToUnixTimeSeconds()}:F>\n\n" +
            $"Use the `/viewapp {applicationDetails.ApplicationId}` command to view more details about this application."
        );
        await Context.Interaction.ModifyResponse(embeds: [embed]);
    }
    
}