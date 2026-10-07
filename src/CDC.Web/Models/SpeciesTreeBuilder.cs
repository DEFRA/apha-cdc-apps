namespace CDC.Web.Models;

/// <summary>
/// Builds a <see cref="TreeNodeViewModel"/> hierarchy from the flat species list returned by
/// <c>GET /api/species</c>, using <c>ParentId</c> to link each node to its parent.
/// </summary>
public static class SpeciesTreeBuilder
{
    /// <summary>A root species has this as its <c>ParentId</c>.</summary>
    private static readonly Guid RootParentId = Guid.Empty;

    /// <summary>
    /// Builds the tree from every <em>active</em> species in <paramref name="species"/>. Root
    /// nodes are returned expanded so the top-level groups are visible without interaction;
    /// deeper levels start collapsed, matching the tree picker's existing default.
    /// </summary>
    /// <param name="species">The flat species list, as returned by the API.</param>
    /// <returns>Root nodes, each with its descendants attached.</returns>
    public static IReadOnlyList<TreeNodeViewModel> Build(IReadOnlyList<SpeciesDto> species) => Build(species, selectedId: null);

    /// <summary>
    /// Builds the tree, additionally forcing every branch on the path to
    /// <paramref name="selectedId"/> open, so a previously-saved selection is visible rather
    /// than hidden inside a collapsed branch.
    /// </summary>
    public static IReadOnlyList<TreeNodeViewModel> Build(IReadOnlyList<SpeciesDto> species, Guid? selectedId)
    {
        ArgumentNullException.ThrowIfNull(species);

        var active = species.Where(item => item.IsActive).ToList();
        var childrenByParentId = active
            .Where(item => item.ParentId != RootParentId)
            .ToLookup(item => item.ParentId);

        // A species whose declared parent is missing or inactive would otherwise vanish
        // silently; treating it as a root keeps every active record visible.
        var activeIds = active.Select(item => item.Id).ToHashSet();
        var roots = active.Where(item => item.ParentId == RootParentId || !activeIds.Contains(item.ParentId));

        var ancestorsOfSelected = AncestorIdsOf(selectedId, active);

        return
        [
            .. roots
                .OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
                .Select(item => BuildNode(item, childrenByParentId, expanded: true, ancestorIds: [], ancestorsOfSelected))
        ];
    }

    /// <summary>Every ancestor id of <paramref name="selectedId"/>, so those branches can be forced open.</summary>
    private static HashSet<Guid> AncestorIdsOf(Guid? selectedId, IReadOnlyList<SpeciesDto> active)
    {
        HashSet<Guid> ancestors = [];

        if (selectedId is not Guid id)
        {
            return ancestors;
        }

        var byId = active.ToDictionary(item => item.Id);
        var current = byId.GetValueOrDefault(id);

        while (current is not null && current.ParentId != RootParentId && ancestors.Add(current.ParentId))
        {
            current = byId.GetValueOrDefault(current.ParentId);
        }

        return ancestors;
    }

    private static TreeNodeViewModel BuildNode(
        SpeciesDto species,
        ILookup<Guid, SpeciesDto> childrenByParentId,
        bool expanded,
        HashSet<Guid> ancestorIds,
        HashSet<Guid> ancestorsOfSelected)
    {
        // Guards against a malformed/cyclic ParentId chain causing unbounded recursion.
        if (ancestorIds.Contains(species.Id))
        {
            return new TreeNodeViewModel { Value = species.Id.ToString(), Label = species.Description };
        }

        HashSet<Guid> ownAncestorIds = [.. ancestorIds, species.Id];

        var children = childrenByParentId[species.Id]
            .OrderBy(child => child.Description, StringComparer.OrdinalIgnoreCase)
            .Select(child => BuildNode(child, childrenByParentId, expanded: false, ownAncestorIds, ancestorsOfSelected))
            .ToList();

        return new TreeNodeViewModel
        {
            Value = species.Id.ToString(),
            Label = species.Description,
            Expanded = expanded || ancestorsOfSelected.Contains(species.Id),
            Children = children
        };
    }
}
