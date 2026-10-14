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
    /// Builds the tree from <paramref name="species"/>. Root nodes are returned expanded so the
    /// top-level groups are visible without interaction; deeper levels start collapsed, matching
    /// the tree picker's existing default.
    /// </summary>
    /// <param name="species">The flat species list, as returned by the API.</param>
    /// <param name="includeInactive">
    /// When <see langword="false"/> (the default), only active species are included, matching
    /// every existing tree picker. When <see langword="true"/>, inactive species are kept in
    /// their existing hierarchy position too - rendered with <see cref="TreeNodeViewModel.IsInactive"/>
    /// set - so "Maintain species data" retains their original relationships after inactivation.
    /// </param>
    /// <param name="preserveApiOrder">
    /// When <see langword="false"/> (the default), siblings are sorted alphabetically, matching
    /// every existing tree picker. When <see langword="true"/>, siblings keep the order the API
    /// returned them in - already <c>SequenceNumber</c> order - so "Maintain species data" shows
    /// a "Reorder list" move take visible effect instead of being masked by an alphabetical sort.
    /// </param>
    /// <returns>Root nodes, each with its descendants attached.</returns>
    public static IReadOnlyList<TreeNodeViewModel> Build(
        IReadOnlyList<SpeciesDto> species,
        bool includeInactive = false,
        bool preserveApiOrder = false)
    {
        ArgumentNullException.ThrowIfNull(species);

        var included = includeInactive ? species.ToList() : species.Where(item => item.IsActive).ToList();
        var childrenByParentId = included
            .Where(item => item.ParentId != RootParentId)
            .ToLookup(item => item.ParentId);

        // A species whose declared parent is missing or excluded would otherwise vanish
        // silently; treating it as a root keeps every included record visible.
        var includedIds = included.Select(item => item.Id).ToHashSet();
        var roots = included.Where(item => item.ParentId == RootParentId || !includedIds.Contains(item.ParentId));

        return
        [
            .. Order(roots, preserveApiOrder)
                .Select(item => BuildNode(item, childrenByParentId, expanded: true, preserveApiOrder, ancestorIds: []))
        ];
    }

    private static IEnumerable<SpeciesDto> Order(IEnumerable<SpeciesDto> siblings, bool preserveApiOrder) =>
        preserveApiOrder ? siblings : siblings.OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase);

    private static TreeNodeViewModel BuildNode(
        SpeciesDto species,
        ILookup<Guid, SpeciesDto> childrenByParentId,
        bool expanded,
        bool preserveApiOrder,
        HashSet<Guid> ancestorIds)
    {
        // Guards against a malformed/cyclic ParentId chain causing unbounded recursion.
        if (ancestorIds.Contains(species.Id))
        {
            return new TreeNodeViewModel { Value = species.Id.ToString(), Label = species.Description, IsInactive = !species.IsActive };
        }

        HashSet<Guid> ownAncestorIds = [.. ancestorIds, species.Id];

        var children = Order(childrenByParentId[species.Id], preserveApiOrder)
            .Select(child => BuildNode(child, childrenByParentId, expanded: false, preserveApiOrder, ownAncestorIds))
            .ToList();

        return new TreeNodeViewModel
        {
            Value = species.Id.ToString(),
            Label = species.Description,
            IsInactive = !species.IsActive,
            Expanded = expanded,
            Children = children
        };
    }
}
