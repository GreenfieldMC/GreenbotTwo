namespace GreenbotTwo.Models.GreenfieldApi;

public class Application
{
    public required long ApplicationId { get; set; }
    public required List<ApplicationStatus> BuildAppStatuses { get; set; }
    public required long UserId { get; set; }
    public required int Age { get; set; }
    public string? Nationality { get; set; }
    public required List<ApplicationImage> Images { get; set; }
    public string? AdditionalBuildingInformation { get; set; }
    public required string WhyJoinGreenfield { get; set; }
    public string? AdditionalComments { get; set; }
    public required DateTime CreatedOn { get; set; }
    
}

public record ApplicationStatus(string Status, string? StatusMessage = null, DateTime CreatedOn = default);

public record ApplicationImage(long ImageLinkId, string Link, string ImageType, DateTime CreatedOn);
public record LatestApplicationStatus(long ApplicationId, ApplicationStatus? LatestStatus);