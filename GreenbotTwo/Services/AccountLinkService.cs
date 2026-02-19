using System;
using System.Collections.Concurrent;
using GreenbotTwo.Embeds;
using GreenbotTwo.Extensions;
using GreenbotTwo.Interactions.AccountLink;
using GreenbotTwo.Interactions.BuildApplications;
using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
using GreenbotTwo.Services.Interfaces;
using NetCord;
using NetCord.Rest;
using User = GreenbotTwo.Models.GreenfieldApi.User;

namespace GreenbotTwo.Services;

public class AccountLinkService(IGreenfieldApiService apiService) : IAccountLinkService
{
    private static readonly IDictionary<ulong, AccountLinkForm> ActiveAccountLinkForms = new ConcurrentDictionary<ulong, AccountLinkForm>();
    private static readonly IDictionary<ulong, (User User, DateTimeOffset ExpiresAt)> CachedVerifiedUsers = new ConcurrentDictionary<ulong, (User, DateTimeOffset)>();
    private static readonly TimeSpan CachedUserTtl = TimeSpan.FromMinutes(10);

    public bool ClearInProgressAccountLink(ulong discordId)
    {
        return ActiveAccountLinkForms.Remove(discordId);
    }

    public bool HasAccountLinkInProgress(ulong discordId)
    {
        return ActiveAccountLinkForms.ContainsKey(discordId);
    }

    public AccountLinkForm GetOrStartAccountLinkForm(ulong discordId, UserSelectionFor source)
    {
        if (ActiveAccountLinkForms.TryGetValue(discordId, out var existingForm))
            return existingForm;
        
        var newForm = new AccountLinkForm(discordId, source);
        ActiveAccountLinkForms[discordId] = newForm;
        return newForm;
    }

    public Task<ComponentContainerProperties> GenerateFinishLinkingComponent(bool disableButton = false)
    {
        var container = new ComponentContainerProperties([
            new ComponentSectionProperties(new ButtonProperties("finish_linking_account", "Finish Linking Account", ButtonStyle.Success).WithDisabled(disableButton), [new TextDisplayProperties("Let's finish linking your account.")])
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
                ICustomizableButtonProperties button = !disconnectUrlResult.TryGetDataNonNull(out var disconnectUrl) 
                    ? new ButtonProperties("__danger__", $"Unlink {dConn.DiscordUsername}", ButtonStyle.Secondary).WithDisabled() 
                    : new LinkButtonProperties(disconnectUrl, $"Unlink {dConn.DiscordUsername}");

                return new ComponentSectionProperties(button).WithComponents([
                    new TextDisplayProperties(dConn.DiscordSnowflake.Mention())
                ]);
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
                ICustomizableButtonProperties button = !disconnectUrlResult.TryGetDataNonNull(out var disconnectUrl) 
                    ? new ButtonProperties("__danger__", $"Unlink {pConn.FullName}", ButtonStyle.Secondary).WithDisabled() 
                    : new LinkButtonProperties(disconnectUrl, $"Unlink {pConn.FullName}");
                
                return new ComponentSectionProperties(button).WithComponents([
                    new TextDisplayProperties($"{pConn.FullName} (Pledge: {pConn.Pledge / 100m:C})")
                ]);
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

    public Task<ComponentContainerProperties> GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor mode, List<User> users)
    {
        string userSelectionMenuId;
        string newAccountButtonId;
        string noLinkedUsersMessage;
        
        switch (mode)
        {
            case UserSelectionFor.Application:
                userSelectionMenuId = ApplyInteractions.ApplicationUserSelectionButton;
                newAccountButtonId = ApplyInteractions.ApplicationLinkNewAccountButton;
                noLinkedUsersMessage = "Thank you for taking interest in applying to our build team! Before continuing with the application, we need you to verify you have a valid Minecraft account and we need to link your current Discord account to that Minecraft account. Press the button to get started!";
                break;
            case UserSelectionFor.AccountView:
                userSelectionMenuId = AccountLinkInteractions.AccountViewUserSelectionButton;
                newAccountButtonId = AccountLinkInteractions.AccountLinkNewAccountButton;
                noLinkedUsersMessage = "You do not have any linked Minecraft accounts.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
        
        
        var container = new ComponentContainerProperties();
        if (users.Count == 0)
        {
            container.AddComponents(
                new TextDisplayProperties(noLinkedUsersMessage), 
                new ActionRowProperties([new ButtonProperties(newAccountButtonId, "Link a new Minecraft account", ButtonStyle.Primary)]));
        }
        else
        {
            container.AddComponents([
                new TextDisplayProperties("Select an existing account to continue, or link a new Minecraft account."),
                new StringMenuProperties(userSelectionMenuId, users.Select(u => new StringMenuSelectOptionProperties(u.Username, u.UserId.ToString())).ToList())
                    .WithRequired()
                    .WithMaxValues(1)
                    .WithMinValues(1),
                new ActionRowProperties([new ButtonProperties(newAccountButtonId, "Link a new Minecraft account", ButtonStyle.Primary)])
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

    public User? GetCachedVerifiedUser(ulong discordId)
    {
        if (!CachedVerifiedUsers.TryGetValue(discordId, out var cached))
            return null;
        if (cached.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            CachedVerifiedUsers.Remove(discordId);
            return null;
        }
        return cached.User;
    }

    public void SetCachedVerifiedUser(ulong discordId, User user)
    {
        CachedVerifiedUsers[discordId] = (user, DateTimeOffset.UtcNow.Add(CachedUserTtl));
    }

    public void ClearCachedVerifiedUser(ulong discordId)
    {
        CachedVerifiedUsers.Remove(discordId);
    }
}