using CDC.Common.Contracts;

namespace CDC.Common.Comparison;

/// <summary>The profile data a comparison reads; CDC.Web supplies it over HTTP and CDC.Api straight from its services.</summary>
public interface IComparisonDataSource
{
    /// <summary>Gets the questionnaire structure.</summary>
    Task<ProfileQuestionnaireMetadataDto> GetMetadataAsync(CancellationToken cancellationToken);

    /// <summary>Gets one version's recorded answers for one section.</summary>
    Task<ProfileSectionAnswersDto> GetAnswersAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken);

    /// <summary>Gets the display text of each value in a reference table, keyed by value id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken);

    /// <summary>Gets every species name, keyed by species id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetSpeciesNamesAsync(CancellationToken cancellationToken);
}
