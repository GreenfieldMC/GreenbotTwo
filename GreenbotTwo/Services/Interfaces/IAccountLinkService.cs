using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
using NetCord.Rest;

namespace GreenbotTwo.Services.Interfaces;

public interface IAccountLinkService
{

    public bool ClearInProgressAccountLink(ulong discordId);

    public bool HasAccountLinkInProgress(ulong discordId);
    
    public AccountLinkForm GetOrStartAccountLinkForm(ulong discordId, AccountLinkService.UserSelectionFor source);

    Task<ComponentContainerProperties> GenerateFinishLinkingComponent(bool disableButton = false);
    
    Task<ComponentContainerProperties> GenerateAccountViewComponent(User user, string channelUrl);

    Task<ComponentContainerProperties> GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor mode,
        List<User> users);

    // cache of recently verified minecraft users per discord id (10 minute ttl)
    User? GetCachedVerifiedUser(ulong discordId);
    void SetCachedVerifiedUser(ulong discordId, User user);
    void ClearCachedVerifiedUser(ulong discordId);
}