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
    
    /// <summary>
    /// The latest status of the application, determined by the most recent CreatedOn timestamp in the BuildAppStatuses list. If there are no statuses, this will be null.
    /// </summary>
    public ApplicationStatus? LatestStatus => BuildAppStatuses.OrderByDescending(s => s.CreatedOn).FirstOrDefault();
    
    /// <summary>
    /// If this application's latest status is marked as rejected. False if there are no statuses or if the latest status is not "Rejected".
    /// </summary>
    public bool IsRejected => LatestStatus?.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ?? false;
    
    /// <summary>
    /// If this application's latest status is marked as approved. False if there are no statuses or if the latest status is not "Approved".
    /// </summary>
    public bool IsApproved => LatestStatus?.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase) ?? false;
    
    /// <summary>
    /// If this application's latest status is marked as submission pending. False if there are no statuses or if the latest status is not "SubmissionPending".
    /// </summary>
    public bool IsSubmissionPending => LatestStatus?.Status.Equals("SubmissionPending", StringComparison.OrdinalIgnoreCase) ?? false;
    
    /// <summary>
    /// If this application's latest status is marked as under review. False if there are no statuses or if the latest status is not "UnderReview".
    /// </summary>
    public bool IsUnderReview => LatestStatus?.Status.Equals("UnderReview", StringComparison.OrdinalIgnoreCase) ?? false;
    
}

public record ApplicationStatus(string Status, string? StatusMessage = null, DateTime CreatedOn = default);

public record ApplicationImage(long ImageLinkId, string Link, string ImageType, DateTime CreatedOn);
public record LatestApplicationStatus(long ApplicationId, ApplicationStatus? LatestStatus);