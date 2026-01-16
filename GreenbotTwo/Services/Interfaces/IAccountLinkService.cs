using GreenbotTwo.Models;
using GreenbotTwo.Models.Forms;
using GreenbotTwo.Models.GreenfieldApi;
using NetCord.Rest;

namespace GreenbotTwo.Services.Interfaces;

public interface IAccountLinkService
{

    public bool ClearInProgressAccountLink(ulong discordId);

    public bool HasAccountLinkInProgress(ulong discordId);
    
    public AccountLinkForm GetOrStartAccountLinkForm(ulong discordId);

    Task<ComponentContainerProperties> GenerateFinishLinkingComponent();
    
    Task<ComponentContainerProperties> GenerateAccountViewComponent(User user, string channelUrl);

    Task<ComponentContainerProperties> GenerateUserSelectionComponent(AccountLinkService.UserSelectionFor mode,
        List<User> users);

}