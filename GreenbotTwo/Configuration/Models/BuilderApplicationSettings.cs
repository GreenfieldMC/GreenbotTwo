using Microsoft.Extensions.Options;

namespace GreenbotTwo.Configuration.Models;

public class BuilderApplicationSettings : IValidateOptions<BuilderApplicationSettings>
{
    public required ulong GuildId { get; init; }
    public required ulong ReviewChannelId { get; init; }
    public required ulong TestBuildRoleId { get; init; }
    public required ulong TestBuilderChannelId { get; init; }
    public required ulong ApplicationStorageChannelId { get; init; }
    public required int MinimumNumberOfHouseImages { get; init; }
    public required int MaximumNumberOfHouseImages { get; init; }
    public required int MinimumNumberOfOtherImages { get; init; }
    public required int MaximumNumberOfOtherImages { get; init; }
    public required int MaximumWhyJoinLength { get; init; }
    public required int MaximumAdditionalBuildingInfoLength { get; init; }
    public required int MaximumAdditionalCommentsLength { get; init; }
    public required long MaximumPerFileSizeBytes { get; init; }
    
    public ValidateOptionsResult Validate(string? name, BuilderApplicationSettings options)
    {
        var failures = new List<string>();
        
        if (options.MinimumNumberOfHouseImages < 0) failures.Add("MinimumNumberOfHouseImages cannot be negative.");
        if (options.MaximumNumberOfHouseImages < options.MinimumNumberOfHouseImages) failures.Add("MaximumNumberOfHouseImages cannot be less than MinimumNumberOfHouseImages.");
        if (options.MinimumNumberOfHouseImages > 10 || options.MaximumNumberOfHouseImages > 10) failures.Add("Number of house images cannot exceed 10.");
        if (options.MinimumNumberOfOtherImages < 0) failures.Add("MinimumNumberOfOtherImages cannot be negative.");
        if (options.MaximumNumberOfOtherImages < options.MinimumNumberOfOtherImages) failures.Add("MaximumNumberOfOtherImages cannot be less than MinimumNumberOfOtherImages.");
        if (options.MinimumNumberOfOtherImages > 10 || options.MaximumNumberOfOtherImages > 10) failures.Add("Number of other images cannot exceed 10.");
        
        if (options.MinimumNumberOfHouseImages + options.MinimumNumberOfOtherImages > 10) failures.Add("The combined minimum number of images cannot exceed 10.");
        if (options.MaximumNumberOfHouseImages + options.MaximumNumberOfOtherImages > 10) failures.Add("The combined maximum number of images cannot exceed 10.");
        
        var freeformTextLength = options.MaximumWhyJoinLength + options.MaximumAdditionalBuildingInfoLength + options.MaximumAdditionalCommentsLength;
        if (freeformTextLength > 3000) failures.Add("The combined length of freeform text fields cannot exceed 3000 characters.");
        
        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    public string GetMaxFileSizeNice()
    {
        return MaximumPerFileSizeBytes switch
        {
            >= 1 << 20 => $"{MaximumPerFileSizeBytes / (1 << 20)} MB",
            >= 1 << 10 => $"{MaximumPerFileSizeBytes / (1 << 10)} KB",
            _ => $"{MaximumPerFileSizeBytes} Bytes"
        };
    }
    
}
