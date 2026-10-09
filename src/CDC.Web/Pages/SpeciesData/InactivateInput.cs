using System.ComponentModel.DataAnnotations;

namespace CDC.Web.Pages.SpeciesData;

/// <summary>The "Inactivate" form fields.</summary>
public sealed class InactivateInput
{
    /// <summary>Gets or sets the species being inactivated.</summary>
    public Guid SpeciesId { get; set; }

    /// <summary>Gets or sets the reason given for the change.</summary>
    [Display(Name = "Reason for change")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the row version last read for this species, base64-encoded for the hidden field.</summary>
    public string LastUpdatedBase64 { get; set; } = string.Empty;
}
