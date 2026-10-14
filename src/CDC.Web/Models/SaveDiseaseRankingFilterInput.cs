using System.ComponentModel.DataAnnotations;

namespace CDC.Web.Models;

/// <summary>
/// Form fields for creating or updating a disease ranking filter, posted from
/// <c>DiseaseRanking/EditFilters.cshtml</c>. Mirrors the legacy
/// <c>EditDiseaseRankingFilters.aspx</c> report title, radio criteria and transmissible agent
/// checkboxes.
/// </summary>
public sealed class SaveDiseaseRankingFilterInput
{
    /// <summary>Matches <c>DiseaseFilters.DiseaseName</c>'s <c>nvarchar(500)</c> column.</summary>
    public const int DiseaseNameMaxLength = 500;

    /// <summary>Gets or sets the filter being updated; <see langword="null"/> when creating a new filter.</summary>
    public long? FilterNumber { get; set; }

    /// <summary>Gets or sets the report title.</summary>
    [Required(ErrorMessage = "Enter a report title")]
    [StringLength(DiseaseNameMaxLength, ErrorMessage = "The report title must be {1} characters or fewer")]
    public string? DiseaseName { get; set; }

    /// <summary>Gets or sets the endemic/exotic/both criterion.</summary>
    [Required(ErrorMessage = "Select endemic diseases, exotic diseases or both")]
    public string? Disease1 { get; set; }

    /// <summary>Gets or sets the zoonotic/non-zoonotic/both criterion.</summary>
    [Required(ErrorMessage = "Select zoonotic, non-zoonotic or both")]
    public string? Disease2 { get; set; }

    /// <summary>Gets or sets the notifiable/non-notifiable/both criterion.</summary>
    [Required(ErrorMessage = "Select notifiable, non-notifiable or both")]
    public string? Disease3 { get; set; }

    /// <summary>Gets or sets the infectious/non-infectious/both criterion.</summary>
    [Required(ErrorMessage = "Select infectious, non-infectious or both")]
    public string? Disease4 { get; set; }

    /// <summary>Gets or sets the selected transmissible agent types, for pre-populating the checkboxes.
    /// Not required when Disease4 is "non-infectious". Not bound on POST - ASP.NET Core doesn't
    /// reliably bind a non-indexed repeated-key collection nested inside another bound complex
    /// object, so the posted values are read via <see cref="EditFiltersModel.SelectedDiseaseTypes"/>
    /// instead.</summary>
    public IReadOnlyList<string> DiseaseTypes { get; set; } = [];
}
