using NetCord.Rest;

namespace GreenbotTwo.Models;

public record StandardBuildAppFailureMessage(string Id, string Label, string Message)
{

    public static IEnumerable<StandardBuildAppFailureMessage> AllFailureMessages =>
    [
        new ("reason_no_builds", "No Minecraft builds provided.",
            "You have not provided any Minecraft builds for us to base our decision on. Please reapply and provide Minecraft builds."),
        new ("reason_underage", "Applicant is underage.",
            "Our services, and discord, are only available to individuals who are at least 13 years old."),
        new ("reason_unrealistic", "Unrealistic builds provided.",
            "The Minecraft builds you have provided do not seem realistic. Please reapply with more realistic builds."),
        new ("reason_too_few_builds", "Not enough builds provided.",
            "You have not provided enough Minecraft builds for us to base our decision on. Please reapply and provide more builds."),
        new ("reason_non_english", "Non-English speaking applicant.",
            "English fluency is a requirement on this project to ensure effective communication within our community."),
        new ("reason_no_american_builds", "No American-styled builds provided.",
            "You have not provided any American-styled Minecraft builds for us to base our decision on. Please reapply and provide American-styled builds."),
        new ("reason_poor_quality", "Poor quality builds provided.",
            "The Minecraft builds you have provided do not meet our quality standards. We recommend you visit our building help channel for guidance before applying in the future.")
    ];
    
    public static StandardBuildAppFailureMessage FromId(string id) => AllFailureMessages.First(x => x.Id == id);

    public CheckboxGroupOptionProperties ToCheckboxOption() => new(Label, Id);

}