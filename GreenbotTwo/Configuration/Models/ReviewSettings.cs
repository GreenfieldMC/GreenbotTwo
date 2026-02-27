namespace GreenbotTwo.Configuration.Models;

public class ReviewSettings
{
    public required IEnumerable<ulong> RolesThatCanApprove { get; init; }
    public required IEnumerable<ulong> RolesThatCanDeny { get; init; }
    public required IEnumerable<ulong> RolesThatCanRefresh { get; init; }
}

