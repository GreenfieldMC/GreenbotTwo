using NetCord;
using NetCord.Rest;

namespace GreenbotTwo.Models;

public class BuilderApplicationForm(ulong discordId)
{
    public Dictionary<ApplicationSections, bool> SectionsCompleted { get; set; } = new()
    {
        { ApplicationSections.PersonalInformation, false },
        { ApplicationSections.BuildingExperience, false },
        { ApplicationSections.TermsOfService, false },
        { ApplicationSections.ClosingRemarks, false }
    };

    public bool Submitted { get; set; } = false;
    public ulong DiscordId { get; set; } = discordId;
    public MinecraftSlimProfile? MinecraftProfile { get; set; }
    public int Age { get; set; } = -1;
    public string? Nationality { get; set; }
    public List<string> HouseBuildLinks { get; set; } = [];
    public List<string> OtherBuildLinks { get; set; } = [];
    public string? AdditionalBuildingInformation { get; set; }
    public string WhyJoinGreenfield { get; set; } = string.Empty;
    public string? AdditionalComments { get; set; }

    public bool IsComplete()
    {
        return SectionsCompleted.All(section => section.Value);
    }
    
    public IEnumerable<ButtonProperties> GenerateButtonsForApplication()
    {

        var section1 = new ButtonProperties("apply_terms", "Section 1 - Terms and Conditions", ButtonStyle.Primary);
        var section2 = new ButtonProperties("apply_user_info", "Section 2 - Personal Information", ButtonStyle.Secondary)
            .WithDisabled();
        var section3 = new ButtonProperties("apply_building_experience", "Section 3 - Experience", ButtonStyle.Secondary)
            .WithDisabled();
        var section4 = new ButtonProperties("apply_closing_thoughts", "Section 4 - Closing Remarks", ButtonStyle.Secondary)
            .WithDisabled();
        var submitButton = new ButtonProperties("apply_final_submit", "Submit Application", ButtonStyle.Secondary)
            .WithDisabled();

        if (SectionsCompleted[ApplicationSections.TermsOfService])
        {
            section1.WithStyle(ButtonStyle.Success).WithEmoji(EmojiProperties.Standard("✅")).WithLabel("Section 1");
            section2.WithDisabled(false).WithStyle(ButtonStyle.Primary);
        }
        
        if (SectionsCompleted[ApplicationSections.PersonalInformation])
        {
            section2.WithStyle(ButtonStyle.Success).WithEmoji(EmojiProperties.Standard("✅")).WithLabel("Section 2");
            section3.WithDisabled(false).WithStyle(ButtonStyle.Primary);
        }
        
        if (SectionsCompleted[ApplicationSections.BuildingExperience])
        {
            section3.WithStyle(ButtonStyle.Success).WithEmoji(EmojiProperties.Standard("✅")).WithLabel("Section 3");
            section4.WithDisabled(false).WithStyle(ButtonStyle.Primary);
        }
        
        if (SectionsCompleted[ApplicationSections.ClosingRemarks])
        {
            section4.WithStyle(ButtonStyle.Success).WithEmoji(EmojiProperties.Standard("✅")).WithLabel("Section 4");
            submitButton.WithDisabled(false).WithStyle(ButtonStyle.Primary);
        }
        
        if (Submitted)
        {
            submitButton.WithDisabled().WithStyle(ButtonStyle.Success).WithEmoji(EmojiProperties.Standard("✅")).WithLabel("Submitted!");
            section4.WithDisabled().WithStyle(ButtonStyle.Success);
            section3.WithDisabled().WithStyle(ButtonStyle.Success);
            section2.WithDisabled().WithStyle(ButtonStyle.Success);
            section1.WithDisabled().WithStyle(ButtonStyle.Success);
        }
        
        if (!IsComplete())
        {
            submitButton.WithStyle(ButtonStyle.Secondary).WithDisabled();
        }
        
        return
        [
            section1,
            section2,
            section3,
            section4,
            submitButton
        ];
    }
    
    public enum ApplicationSections
    {
        TermsOfService,
        PersonalInformation,
        BuildingExperience,
        ClosingRemarks
    }
    
}