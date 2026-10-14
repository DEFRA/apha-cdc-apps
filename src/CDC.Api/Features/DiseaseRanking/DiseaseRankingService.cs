using System.Text.RegularExpressions;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Features.Species.Interfaces;
using CDC.Common.Contracts;

namespace CDC.Api.Features.DiseaseRanking;

/// <summary>
/// Default <see cref="IDiseaseRankingService"/>. Applies the terrestrial/aquatic split and the
/// report layout the legacy <c>DiseaseRanking.aspx</c> and its PDF report classes applied.
/// </summary>
/// <param name="repository">Disease ranking data access.</param>
/// <param name="speciesRepository">Species data access, used to tell aquatic species from terrestrial ones.</param>
/// <param name="logger">Structured logger.</param>
public sealed partial class DiseaseRankingService(
    IDiseaseRankingRepository repository,
    ISpeciesRepository speciesRepository,
    ILogger<DiseaseRankingService> logger) : IDiseaseRankingService
{
    /// <summary>The report key for terrestrial diseases.</summary>
    public const string TerrestrialKey = "terrestrial";

    /// <summary>The report key for aquatic diseases.</summary>
    public const string AquaticKey = "aquatic";

    private static readonly string[] AquaticRootSpecies = ["Fish", "Molluscs"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken)
    {
        var filters = await repository.GetFiltersAsync(cancellationToken);

        return
        [
            new DiseaseRankingReportOptionDto { Key = TerrestrialKey, Label = "Disease ranking report: Terrestrial diseases" },
            new DiseaseRankingReportOptionDto { Key = AquaticKey, Label = "Disease ranking report: Aquatic diseases" },
            .. filters.Select(filter => new DiseaseRankingReportOptionDto
            {
                Key = filter.Id.ToString(),
                Label = FormatFilterName(filter.Name)
            })
        ];
    }

    /// <inheritdoc />
    public async Task<DiseaseRankingReportDto?> GetReportAsync(
        string reportKey,
        DiseaseRankingReportKind kind,
        CancellationToken cancellationToken)
    {
        var normalizedReportKey = reportKey.Trim();
        var selection = await SelectProfilesAsync(normalizedReportKey, cancellationToken);

        if (selection is null)
        {
            logger.UnknownReport(normalizedReportKey);
            return null;
        }

        var (scope, profiles) = selection.Value;

        logger.BuiltReport(kind, normalizedReportKey, profiles.Count);

        return DiseaseRankingReportBuilder.Build(kind, scope, profiles);
    }

    [GeneratedRegex("<.*?>")]
    private static partial Regex MarkupRegex();

    // Legacy DiseaseFilterValues.FormatName: strip markup and sentence-case the name.
    private static string FormatFilterName(string name)
    {
        var cleaned = MarkupRegex().Replace(name.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase), string.Empty).Trim();

        return cleaned.Length == 0
            ? cleaned
            : char.ToUpperInvariant(cleaned[0]) + cleaned[1..].ToLowerInvariant();
    }

    private async Task<(DiseaseRankingScope Scope, IReadOnlyList<RankedProfile> Profiles)?> SelectProfilesAsync(
        string reportKey,
        CancellationToken cancellationToken)
    {
        var normalizedReportKey = reportKey.Trim();

        if (string.Equals(normalizedReportKey, TerrestrialKey, StringComparison.OrdinalIgnoreCase))
        {
            return (new DiseaseRankingScope("Terrestrial Diseases", "Terrestrial"), await GetBySpeciesGroupAsync(isAquatic: false, cancellationToken));
        }

        if (string.Equals(normalizedReportKey, AquaticKey, StringComparison.OrdinalIgnoreCase))
        {
            return (new DiseaseRankingScope("Aquatic Diseases", "Aquatic"), await GetBySpeciesGroupAsync(isAquatic: true, cancellationToken));
        }

        if (!Guid.TryParse(normalizedReportKey, out var filterId))
        {
            return null;
        }

        var filters = await repository.GetFiltersAsync(cancellationToken);
        var filter = filters.FirstOrDefault(candidate => candidate.Id == filterId);

        if (filter is null)
        {
            return null;
        }

        var name = FormatFilterName(filter.Name);

        return (new DiseaseRankingScope(name, name), await repository.GetRankedProfilesAsync(filter.Name, cancellationToken));
    }

    private async Task<IReadOnlyList<RankedProfile>> GetBySpeciesGroupAsync(bool isAquatic, CancellationToken cancellationToken)
    {
        var profiles = await repository.GetRankedProfilesAsync(null, cancellationToken);
        var species = (await speciesRepository.GetAllSpeciesAsync(cancellationToken)).ToDictionary(item => item.Id);

        return [.. profiles.Where(profile => IsAquatic(profile.SpeciesId, species) == isAquatic)];
    }

    // A species is aquatic when the root of its parent chain is the Fish or Molluscs group.
    private static bool IsAquatic(Guid speciesId, Dictionary<Guid, Domain.Entities.Species> species)
    {
        if (!species.TryGetValue(speciesId, out var current))
        {
            return false;
        }

        // The hop limit stops a malformed (cyclic) hierarchy from looping forever.
        for (var hops = 0; hops < species.Count && current.ParentId != Guid.Empty && species.TryGetValue(current.ParentId, out var parent); hops++)
        {
            current = parent;
        }

        return AquaticRootSpecies.Contains(current.Description, StringComparer.Ordinal);
    }
}
