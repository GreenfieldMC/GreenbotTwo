namespace GreenbotTwo.Models.GreenfieldApi;

public class ResourcePackBranch
{
    public required string Name { get; init; }
    public required ResourcePackBranchCommit Commit { get; init; }
}

public class ResourcePackBranchCommit
{
    public required string Sha { get; init; }
}

public class ResourcePackDownloadRequest
{
    public required string DownloadUrl { get; init; }
    public required Guid Token { get; init; }
    public required int ExpiresInMinutes { get; init; }
}

