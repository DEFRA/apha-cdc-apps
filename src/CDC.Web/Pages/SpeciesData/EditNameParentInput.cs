using System.ComponentModel.DataAnnotations;

namespace CDC.Web.Pages.SpeciesData;

/// <summary>The "Edit name/parent" form fields.</summary>
public sealed class EditNameParentInput
{
    /// <summary>Gets or sets the species being edited.</summary>
    public Guid SpeciesId { get; set; }

    /// <summary>Gets or sets the new display name.</summary>
    [Display(Name = "New name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets the new parent identifier; <see langword="null"/> for a root species.</summary>
    [Display(Name = "New parent")]
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the reason given for the change.</summary>
    [Display(Name = "Reason for change")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the row version last read for this species, base64-encoded for the hidden field.</summary>
    public string LastUpdatedBase64 { get; set; } = string.Empty;
}
