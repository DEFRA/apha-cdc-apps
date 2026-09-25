namespace CDC.Web.Models;

/// <summary>Form-bound input for the "Create and maintain disease ranking filters" page.</summary>
public sealed class DiseaseRankingFilterInput
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }

    public Guid? SpeciesId { get; set; }

    public string? DiseaseType { get; set; }

    public string? ZoonoticStatus { get; set; }

    public string? NotifiableStatus { get; set; }

    public string? InfectiousStatus { get; set; }

    public List<string> InfectiousAgents { get; set; } = [];

    public static DiseaseRankingFilterInput FromFilter(DiseaseRankingFilter filter) => new()
    {
        Id = filter.Id,
        Name = filter.Name,
        SpeciesId = filter.SpeciesId,
        DiseaseType = filter.DiseaseType,
        ZoonoticStatus = filter.ZoonoticStatus,
        NotifiableStatus = filter.NotifiableStatus,
        InfectiousStatus = filter.InfectiousStatus,
        InfectiousAgents = [.. filter.InfectiousAgents]
    };

    public DiseaseRankingFilter ToFilter(Guid id) => new()
    {
        Id = id,
        Name = Name!.Trim(),
        SpeciesId = SpeciesId!.Value,
        DiseaseType = DiseaseType!,
        ZoonoticStatus = ZoonoticStatus!,
        NotifiableStatus = NotifiableStatus!,
        InfectiousStatus = InfectiousStatus!,
        InfectiousAgents = InfectiousStatus == "non-infectious" ? [] : InfectiousAgents
    };

    /// <summary>
    /// Validates the input, returning one entry per problem. The field id in each entry matches
    /// the id of the corresponding input on the page, so the error summary can link straight to it.
    /// </summary>
    public IReadOnlyList<(string FieldId, string Message)> Validate()
    {
        List<(string FieldId, string Message)> errors = [];

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add(("Input_Name", "Enter a filter name"));
        }

        if (SpeciesId is null)
        {
            errors.Add(("species-tree-status", "Select a species or species group"));
        }

        if (string.IsNullOrEmpty(DiseaseType))
        {
            errors.Add(("Input_DiseaseType", "Select endemic diseases, exotic diseases, or both"));
        }

        if (string.IsNullOrEmpty(ZoonoticStatus))
        {
            errors.Add(("Input_ZoonoticStatus", "Select zoonotic, non-zoonotic, or both"));
        }

        if (string.IsNullOrEmpty(NotifiableStatus))
        {
            errors.Add(("Input_NotifiableStatus", "Select notifiable, non-notifiable, or both"));
        }

        if (string.IsNullOrEmpty(InfectiousStatus))
        {
            errors.Add(("Input_InfectiousStatus", "Select infectious, non-infectious, or both"));
        }
        else if (InfectiousStatus != "non-infectious" && InfectiousAgents.Count == 0)
        {
            errors.Add(("Input_InfectiousAgents", "Select at least one type of infectious agent"));
        }

        return errors;
    }
}
