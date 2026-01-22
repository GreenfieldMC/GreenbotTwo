using System.Collections.Concurrent;
using GreenbotTwo.Embeds;
using GreenbotTwo.Interactions.AccountLink;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using User = GreenbotTwo.Models.GreenfieldApi.User;

namespace GreenbotTwo.Services;

public class AccountLinkService(IGreenfieldApiService apiService) : IAccountLinkService
{
    
    private static readonly IDictionary<ulong, AccountLinkForm> ActiveAccountLinkForms = new ConcurrentDictionary<ulong, AccountLinkForm>();

    public bool ClearInProgressAccountLink(ulong discordId)
    {
        return ActiveAccountLinkForms.Remove(discordId);
    }

    public bool HasAccountLinkInProgress(ulong discordId)
    {
        return ActiveAccountLinkForms.ContainsKey(discordId);
    }

    public AccountLinkForm GetOrStartAccountLinkForm(ulong discordId)
    {
        if (ActiveAccountLinkForms.TryGetValue(discordId, out var existingForm))
            return existingForm;
        
        var newForm = new AccountLinkForm(discordId);
        ActiveAccountLinkForms[discordId] = newForm;
        return newForm;
    }

    public Task<ComponentContainerProperties> GenerateFinishLinkingComponent()
    {
        var container = new ComponentContainerProperties([
            new ComponentSectionProperties(new ButtonProperties("finish_linking_account", "Finish Linking Account", ButtonStyle.Success), [new TextDisplayProperties("Let's finish linking your account.")])
        ]);
        container.WithAccentColor(ColorHelpers.Success);
        return Task.FromResult(container);
    }

    public async Task<ComponentContainerProperties> GenerateAccountViewComponent(User user, string channelUrl)
    {
        var foundProfilesResult = await apiService.GetDiscordAccountsForUser(user.UserId);
        if (!foundProfilesResult.TryGetDataNonNull(out var foundProfiles))
            foundProfiles = [];
        
        var componentList = new List<IComponentContainerComponentProperties>();

        componentList.Add(new TextDisplayProperties($"## Minecraft Account Information\n**Selected User:** `{user.Username}` ~~--~~ **UUID:** `{user.MinecraftUuid}`"));
        componentList.Add(new ComponentSeparatorProperties());
        componentList.Add(new TextDisplayProperties("### Attached Discord Profiles"));
        
        if (foundProfiles.Count == 0) 
            componentList.Add(new TextDisplayProperties("*There are no Discord profiles linked.*"));
        else
            componentList.AddRange(foundProfiles.Select(dConn => 
            {
                var disconnectUrlResult = apiService.GetDiscordDisconnectUrl(user.UserId, dConn.DiscordConnectionId, channelUrl).GetAwaiter().GetResult();
                return !disconnectUrlResult.TryGetDataNonNull(out var disconnectUrl) 
                    ? new ActionRowProperties([new ButtonProperties("__danger__", $"Unlink {dConn.DiscordUsername}", ButtonStyle.Secondary).WithDisabled()]) 
                    : new ActionRowProperties([new LinkButtonProperties(disconnectUrl, $"Unlink {dConn.DiscordUsername}")]);
            }));
        
        componentList.Add(new ComponentSeparatorProperties());
        
        var foundPatronsResult = await apiService.GetPatronAccountsForUser(user.UserId);
        if (!foundPatronsResult.TryGetDataNonNull(out var foundPatrons))
            foundPatrons = [];
        
        componentList.Add(new TextDisplayProperties("### Attached Patreon Profiles"));

        if (foundPatrons.Count == 0)
            componentList.Add(new TextDisplayProperties("*There are no Patreon profiles linked.*"));
        else
            componentList.AddRange(foundPatrons.Select(pConn =>
            {
                var disconnectUrlResult = apiService.GetPatreonDisconnectUrl(user.UserId, pConn.PatreonConnectionId, channelUrl).GetAwaiter().GetResult();
                return !disconnectUrlResult.TryGetDataNonNull(out var disconnectUrl) 
                    ? new ActionRowProperties([new ButtonProperties("__danger__", $"Unlink {pConn.FullName}", ButtonStyle.Secondary).WithDisabled()]) 
                    : new ActionRowProperties([new LinkButtonProperties(disconnectUrl, $"Unlink {pConn.FullName}")]);
            }));
        
        componentList.Add(new ComponentSeparatorProperties());
        
        var row = new ActionRowProperties();
        
        var discordConnectUrlResult = await apiService.GetDiscordConnectUrl(user.UserId, channelUrl);
        if (discordConnectUrlResult.TryGetDataNonNull(out var discordConnectUrl)) 
            row.AddComponents([new LinkButtonProperties(discordConnectUrl, "Link New Discord Account")]);
        
        var patreonConnectUrlResult = await apiService.GetPatreonConnectUrl(user.UserId, channelUrl);
        if (patreonConnectUrlResult.TryGetDataNonNull(out var patreonConnectUrl))
            row.AddComponents([new LinkButtonProperties(patreonConnectUrl, "Link New Patreon Account")]);
        
        componentList.Add(row);
        
        return new ComponentContainerProperties()
            .WithComponents(componentList)
            .WithAccentColor(ColorHelpers.Success);
    }

    public Task<ComponentContainerProperties> GenerateUserSelectionComponent(UserSelectionFor mode, List<User> users)
    {
        var menuId = mode switch
        {
            UserSelectionFor.AccountView => AccountLinkInteractions.AccountViewUserSelectionButton,
            UserSelectionFor.Application => ApplyInteractions.ApplicationUserSelectionButton,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        
        var noLinkedUsersMessage = mode switch
        {
            UserSelectionFor.AccountView => "You do not have any linked Minecraft accounts.",
            UserSelectionFor.Application => "Thank you for taking interest in applying to our build team! Before continuing with the application, we need you to verify you have a valid Minecraft account and we need to link your current Discord account to that Minecraft account. Press the `Link a new Minecraft account` button to get started!",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        
        var container = new ComponentContainerProperties();
        if (users.Count == 0)
        {
            container.AddComponents([new ComponentSectionProperties(new ButtonProperties("link_new_account", "Link a new Minecraft account", ButtonStyle.Primary), [new TextDisplayProperties(noLinkedUsersMessage)])]);
        }
        else
        {
            container.AddComponents([
                new StringMenuProperties(menuId, users.Select(u => new StringMenuSelectOptionProperties(u.Username, u.UserId.ToString())).ToList())
                    .WithRequired()
                    .WithMaxValues(1)
                    .WithMinValues(1),
                new ComponentSectionProperties(new ButtonProperties("link_new_account", "Link a new Minecraft account", ButtonStyle.Primary), [new TextDisplayProperties("Select an existing account to continue, or link a new Minecraft account.")])
            ]);
        }
        
        container.WithAccentColor(ColorHelpers.Info);
        return Task.FromResult(container);
    }
    
    public enum UserSelectionFor
    {
        AccountView,
        Application
    }
    
}