using GreenbotTwo.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GreenbotTwo.NetCordSupport.AutocompleteProviders;

public class BranchAutocompleteProvider(IGreenfieldApiService apiService) : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private static readonly MemoryCache BranchCache = new(new MemoryCacheOptions());
    private const string CacheKey = "resource_pack_branches";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option, AutocompleteInteractionContext context)
    {
        var branches = await GetCachedBranchesAsync(apiService);
        var userInput = option.Value ?? string.Empty;

        return branches
            .Where(b => b.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(b => new ApplicationCommandOptionChoiceProperties(b, b));
    }

    private static async Task<List<string>> GetCachedBranchesAsync(IGreenfieldApiService apiService)
    {
        if (BranchCache.TryGetValue(CacheKey, out List<string>? cached) && cached is not null)
            return cached;

        var result = await apiService.GetResourcePackBranches();
        var branchNames = result.TryGetDataNonNull(out var branches)
            ? branches.Select(b => b.Name).ToList()
            : [];

        BranchCache.Set(CacheKey, branchNames, CacheDuration);
        return branchNames;
    }
}

