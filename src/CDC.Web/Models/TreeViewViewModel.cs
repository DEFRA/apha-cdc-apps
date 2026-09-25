namespace CDC.Web.Models;

/// A single node in an app-tree (tree picker) hierarchy.
public sealed record TreeNodeViewModel
{
    /// Submitted value, and the suffix of the checkbox id, so it must be URL and id safe.
    public required string Value { get; init; }

    public required string Label { get; init; }

    /// Branches only: whether the node starts expanded.
    public bool Expanded { get; init; }

    public IReadOnlyList<TreeNodeViewModel> Children { get; init; } = [];
}

/// Root model for the _TreeView partial.
public sealed record TreeViewViewModel
{
    /// Prefix for generated checkbox ids, unique within the page.
    public required string IdPrefix { get; init; }

    /// Name applied to every checkbox, so selections post back as a single collection.
    public required string FieldName { get; init; }

    public required IReadOnlyList<TreeNodeViewModel> Nodes { get; init; }

    /// Used by the live selection count, for example "1 species selected".
    public string ItemNameSingular { get; init; } = "item";

    public string ItemNamePlural { get; init; } = "items";

    /// Value of the node that should render pre-checked, for example when re-displaying a
    /// saved selection. Null means no node is pre-checked. Ignored when
    /// <see cref="AllowMultipleSelection"/> is true; use <see cref="SelectedValues"/> instead.
    public string? SelectedValue { get; init; }

    /// Whether more than one node may be checked at once (checkboxes) rather than exactly one
    /// (radios, the default).
    public bool AllowMultipleSelection { get; init; }

    /// Values that should render pre-checked when <see cref="AllowMultipleSelection"/> is true.
    public IReadOnlySet<string> SelectedValues { get; init; } = new HashSet<string>();

    /// Label describing the empty/no-selection state, for example "Any species".
    public string? EmptySelectionLabel { get; init; }
}
