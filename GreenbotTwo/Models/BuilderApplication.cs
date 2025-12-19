namespace GreenbotTwo.Models;

public class BuilderApplication
{
    public required long ApplicationId { get; set; }
    public required List<BuildAppStatus> BuildAppStatuses { get; set; }
    public required User User { get; set; }
    public required int Age { get; set; }
    public string? Nationality { get; set; }
    public required List<BuildAppImage> HouseBuilds { get; set; }
    public required List<BuildAppImage> OtherBuilds { get; set; }
    public string? AdditionalBuildingInformation { get; set; }
    public required string WhyJoinGreenfield { get; set; }
    public string? AdditionalComments { get; set; }
    public required DateTime CreatedOn { get; set; }
    
}

public record BuildAppStatus(string Status, string? StatusMessage = null, DateTime CreatedOn = default);
public record BuildAppImage(string Link, string ImageType, DateTime CreatedOn);
public record LatestBuildAppStatus(long ApplicationId, BuildAppStatus? LatestStatus);