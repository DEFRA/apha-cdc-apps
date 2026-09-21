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
    public static IReadOnlyList<TreeNodeViewModel> Build(IReadOnlyList<SpeciesDto> species)
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

        return
        [
            .. roots
                .OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
                .Select(item => BuildNode(item, childrenByParentId, expanded: true, ancestorIds: new HashSet<Guid>()))
        ];
    }

    private static TreeNodeViewModel BuildNode(
        SpeciesDto species,
        ILookup<Guid, SpeciesDto> childrenByParentId,
        bool expanded,
        HashSet<Guid> ancestorIds)
    {
        // Guards against a malformed/cyclic ParentId chain causing unbounded recursion.
        if (ancestorIds.Contains(species.Id))
        {
            return new TreeNodeViewModel { Value = species.Id.ToString(), Label = species.Description };
        }

        HashSet<Guid> ownAncestorIds = [.. ancestorIds, species.Id];

        var children = childrenByParentId[species.Id]
            .OrderBy(child => child.Description, StringComparer.OrdinalIgnoreCase)
            .Select(child => BuildNode(child, childrenByParentId, expanded: false, ownAncestorIds))
            .ToList();

        return new TreeNodeViewModel
        {
            Value = species.Id.ToString(),
            Label = species.Description,
            Expanded = expanded,
            Children = children
        };
    }
}
