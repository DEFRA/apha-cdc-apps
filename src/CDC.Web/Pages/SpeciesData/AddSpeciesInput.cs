using System.ComponentModel.DataAnnotations;

namespace CDC.Web.Pages.SpeciesData;

/// <summary>The "Add species data value" form fields.</summary>
public sealed class AddSpeciesInput
{
    /// <summary>Gets or sets the display name of the new species.</summary>
    [Display(Name = "New name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the chosen parent. <see langword="null"/> means nothing was selected;
    /// <see cref="Guid.Empty"/> means the new species sits at the root of the hierarchy.
    /// </summary>
    [Display(Name = "New parent")]
    public Guid? ParentId { get; set; }

    /// <summary>Gets or sets the reason given for the change.</summary>
    [Display(Name = "Reason for change")]
    public string? Reason { get; set; }
}
