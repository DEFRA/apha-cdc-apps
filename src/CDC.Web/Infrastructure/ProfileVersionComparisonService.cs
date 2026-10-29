using CDC.Common.Comparison;
using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Builds the differences-only report for two profile versions.</summary>
public interface IProfileVersionComparisonService
{
    /// <param name="left">The earlier version.</param>
    /// <param name="right">The later version.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<ProfileVersionComparisonReport> BuildReportAsync(
        ProfileVersionOption left,
        ProfileVersionOption right,
        CancellationToken cancellationToken = default);
}

/// <summary>Builds comparison reports with <see cref="ProfileVersionComparisonBuilder"/>, reading profile data from CDC.Api.</summary>
public sealed class ProfileVersionComparisonService(
    IProfileSectionsApiService sectionsApi,
    ISpeciesApiService speciesApi) : IProfileVersionComparisonService, IComparisonDataSource
{
    /// <inheritdoc />
    public Task<ProfileVersionComparisonReport> BuildReportAsync(
        ProfileVersionOption left,
        ProfileVersionOption right,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return ProfileVersionComparisonBuilder.BuildAsync(this, left.VersionId, left.FullLabel, right.VersionId, right.FullLabel, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ProfileQuestionnaireMetadataDto> GetMetadataAsync(CancellationToken cancellationToken) =>
        sectionsApi.GetProfileQuestionnaireMetadataAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ProfileSectionAnswersDto> GetAnswersAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken) =>
        sectionsApi.GetProfileSectionAnswersAsync(profileVersionId, profileSectionId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken)
    {
        var values = await sectionsApi.GetReferenceValuesAsync(referenceTableId, cancellationToken);

        return values.GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.First().Value);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetSpeciesNamesAsync(CancellationToken cancellationToken) =>
        (await speciesApi.GetAllSpeciesAsync(cancellationToken))
            .GroupBy(species => species.Id)
            .ToDictionary(group => group.Key, group => group.First().Description);
}
