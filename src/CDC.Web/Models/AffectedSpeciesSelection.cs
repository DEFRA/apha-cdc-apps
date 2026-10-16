namespace CDC.Web.Models;

/// <summary>The species picked for a profile once the legacy affected species selector's rules have been applied.</summary>
/// <param name="Profiled">The species the profile is about. Never includes a species whose group is also in the list.</param>
/// <param name="Other">The other species the profile affects. Never includes a species whose group is also in the list.</param>
/// <param name="Errors">Why the selection cannot be saved, empty when it can.</param>
public sealed record AffectedSpeciesSelectionResult(
    IReadOnlyList<Guid> Profiled,
    IReadOnlyList<Guid> Other,
    IReadOnlyList<string> Errors);

/// <summary>
/// The rules the legacy <c>AffectedSpeciesSelector</c> control and <c>Profile</c> business object applied
/// to a profile's affected species, and the changes they imply: a group stands in for every species
/// under it, a species is either profiled or "other" but never both, an inactive species can only stay
/// selected, and at least one species must be profiled.
/// </summary>
public static class AffectedSpeciesSelection
{
    public const string NoProfiledSpeciesMessage = "You must specify at least one profiled species";

    public const string UnknownSpeciesMessage = "One or more of the selected species could not be found. Reload the page and try again.";

    /// <summary>
    /// Validates a posted selection and reduces each list to its top-most species, as the legacy
    /// <c>GetProfiledSpecies</c>/<c>GetOtherAffectedSpecies</c> did.
    /// </summary>
    /// <param name="species">Every species and species group, with their parents.</param>
    /// <param name="profiled">The species ticked as profiled.</param>
    /// <param name="other">The species ticked as other affected species.</param>
    /// <param name="alreadySelected">The species the profile already has, which may stay selected even if inactive.</param>
    public static AffectedSpeciesSelectionResult Normalise(
        IReadOnlyList<SpeciesDto> species,
        IEnumerable<Guid> profiled,
        IEnumerable<Guid> other,
        IReadOnlySet<Guid> alreadySelected)
    {
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(profiled);
        ArgumentNullException.ThrowIfNull(other);
        ArgumentNullException.ThrowIfNull(alreadySelected);

        var byId = IndexById(species);
        var requestedProfiled = profiled.Distinct().ToList();
        var requestedOther = other.Distinct().ToList();
        var errors = new List<string>();

        var profiledIds = requestedProfiled.Where(byId.ContainsKey).ToList();
        var otherIds = requestedOther.Where(byId.ContainsKey).ToList();

        if (profiledIds.Count + otherIds.Count < requestedProfiled.Count + requestedOther.Count)
        {
            errors.Add(UnknownSpeciesMessage);
        }

        if (profiledIds.Count == 0)
        {
            errors.Add(NoProfiledSpeciesMessage);
        }

        errors.AddRange(Overlaps(byId, profiledIds, otherIds));
        errors.AddRange(
            profiledIds.Concat(otherIds)
                .Where(id => !byId[id].IsActive && !alreadySelected.Contains(id))
                .Select(id => $"{byId[id].Description} is inactive and cannot be selected"));

        return new AffectedSpeciesSelectionResult(TopMost(byId, profiledIds), TopMost(byId, otherIds), errors);
    }

    /// <summary>Reduces <paramref name="ids"/> to the species that have no ancestor in the same list.</summary>
    public static IReadOnlyList<Guid> TopMost(IReadOnlyList<SpeciesDto> species, IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(species);
        ArgumentNullException.ThrowIfNull(ids);

        return TopMost(IndexById(species), ids.Distinct().ToList());
    }

    /// <summary>
    /// Works out which species to add and remove so a profile's current draft ends up with the selection,
    /// as the legacy page's remove-then-add loop did: a species that changes type is removed then re-added.
    /// </summary>
    public static (IReadOnlyList<AffectedSpeciesInsertDto> Inserts, IReadOnlyList<AffectedSpeciesDeleteDto> Deletes) PlanUpdate(
        Guid draftProfileVersionId,
        IReadOnlyList<AffectedSpeciesDto> stored,
        IReadOnlyList<Guid> profiled,
        IReadOnlyList<Guid> other)
    {
        ArgumentNullException.ThrowIfNull(stored);

        var selected = SelectedPairs(profiled, other);
        var selectedSet = selected.ToHashSet();
        var storedPairs = stored.Select(item => (item.SpeciesId, Type: TypeOf(item))).ToList();
        var storedSet = storedPairs.ToHashSet();

        var deletes = storedPairs
            .Where(pair => !selectedSet.Contains(pair))
            .Select(pair => new AffectedSpeciesDeleteDto { ProfileVersionId = draftProfileVersionId, SpeciesId = pair.SpeciesId })
            .ToList();
        var inserts = selected
            .Where(pair => !storedSet.Contains(pair))
            .Select(pair => Insert(draftProfileVersionId, Guid.Empty, pair))
            .ToList();

        return (inserts, deletes);
    }

    /// <summary>Lists every selected species as an addition to a new profile's first draft version.</summary>
    /// <param name="newDraftProfileVersionId">The new profile's first draft version.</param>
    /// <param name="cloneProfileVersionId">The version being cloned, or <see cref="Guid.Empty"/> for a brand new profile.</param>
    /// <param name="profiled">The species the profile is about.</param>
    /// <param name="other">The other species the profile affects.</param>
    public static IReadOnlyList<AffectedSpeciesInsertDto> PlanCreate(
        Guid newDraftProfileVersionId,
        Guid cloneProfileVersionId,
        IReadOnlyList<Guid> profiled,
        IReadOnlyList<Guid> other) =>
        [.. SelectedPairs(profiled, other).Select(pair => Insert(newDraftProfileVersionId, cloneProfileVersionId, pair))];

    /// <summary>The ids of the species of one type, from a list of affected species.</summary>
    public static IReadOnlyList<Guid> IdsOfType(IEnumerable<AffectedSpeciesDto> affected, string type) =>
        [.. affected.Where(item => TypeOf(item) == type).Select(item => item.SpeciesId).Distinct()];

    private static string TypeOf(AffectedSpeciesDto affected) =>
        affected.Type == AffectedSpeciesTypes.Profiled ? AffectedSpeciesTypes.Profiled : AffectedSpeciesTypes.Other;

    private static List<(Guid SpeciesId, string Type)> SelectedPairs(IReadOnlyList<Guid> profiled, IReadOnlyList<Guid> other) =>
    [
        .. profiled.Select(id => (id, AffectedSpeciesTypes.Profiled)),
        .. other.Select(id => (id, AffectedSpeciesTypes.Other))
    ];

    private static AffectedSpeciesInsertDto Insert(Guid profileVersionId, Guid cloneProfileVersionId, (Guid SpeciesId, string Type) pair) => new()
    {
        ProfileVersionId = profileVersionId,
        CloneProfileVersionId = cloneProfileVersionId,
        SpeciesId = pair.SpeciesId,
        Type = pair.Type
    };

    private static Dictionary<Guid, SpeciesDto> IndexById(IReadOnlyList<SpeciesDto> species) =>
        species.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());

    private static List<Guid> TopMost(Dictionary<Guid, SpeciesDto> byId, List<Guid> ids)
    {
        var selected = ids.ToHashSet();

        return [.. ids.Where(id => !Ancestors(byId, id).Any(selected.Contains))];
    }

    private static IEnumerable<string> Overlaps(Dictionary<Guid, SpeciesDto> byId, List<Guid> profiled, List<Guid> other)
    {
        foreach (var profiledId in profiled)
        {
            var profiledAncestors = Ancestors(byId, profiledId).ToHashSet();

            foreach (var otherId in other)
            {
                if (profiledId == otherId)
                {
                    yield return $"{byId[profiledId].Description} cannot be both a profiled species and another affected species";
                }
                else if (profiledAncestors.Contains(otherId) || Ancestors(byId, otherId).Contains(profiledId))
                {
                    yield return $"{byId[profiledId].Description} and {byId[otherId].Description} belong to the same species group, so they cannot be both profiled and other affected species";
                }
            }
        }
    }

    // The parent chain is walked with a guard, so a malformed cyclic hierarchy cannot loop forever.
    // A species that is not in the list has no known ancestors.
    private static IEnumerable<Guid> Ancestors(Dictionary<Guid, SpeciesDto> byId, Guid id)
    {
        if (!byId.TryGetValue(id, out var species))
        {
            yield break;
        }

        var visited = new HashSet<Guid> { id };
        var current = species.ParentId;

        while (current != Guid.Empty && byId.ContainsKey(current) && visited.Add(current))
        {
            yield return current;
            current = byId[current].ParentId;
        }
    }
}
