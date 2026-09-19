using CDC.Web.Models;

namespace CDC.Web.Pages;

public class ViewSpeciesDataModel : BreadcrumbPageModelBase
{
    private const string SpeciesKey = "species";

    public ViewSpeciesDataModel() : base("Species Data", "View species data")
    {
    }

    /// Placeholder hierarchy until a species data source is implemented; the shape is what a
    /// repository or API would project onto.
    public TreeViewViewModel SpeciesTree { get; } = new()
    {
        IdPrefix = SpeciesKey,
        FieldName = SpeciesKey,
        ItemNameSingular = SpeciesKey,
        ItemNamePlural = SpeciesKey,
        Nodes =
        [
            new TreeNodeViewModel
            {
                Value = "mammals",
                Label = "Mammals (placeholder)",
                Expanded = true,
                Children =
                [
                    new TreeNodeViewModel
                    {
                        Value = "bovine",
                        Label = "Bovine",
                        Children =
                        [
                            new TreeNodeViewModel { Value = "cattle", Label = "Cattle" },
                            new TreeNodeViewModel { Value = "water-buffalo", Label = "Water buffalo" }
                        ]
                    },
                    new TreeNodeViewModel
                    {
                        Value = "ovine-caprine",
                        Label = "Ovine and caprine",
                        Children =
                        [
                            new TreeNodeViewModel { Value = "sheep", Label = "Sheep" },
                            new TreeNodeViewModel { Value = "goat", Label = "Goat" }
                        ]
                    }
                ]
            },
            new TreeNodeViewModel
            {
                Value = "birds",
                Label = "Birds (placeholder)",
                Children =
                [
                    new TreeNodeViewModel
                    {
                        Value = "poultry",
                        Label = "Poultry",
                        Children =
                        [
                            new TreeNodeViewModel { Value = "chicken", Label = "Chicken" },
                            new TreeNodeViewModel { Value = "turkey", Label = "Turkey" }
                        ]
                    },
                    new TreeNodeViewModel
                    {
                        Value = "wild-birds",
                        Label = "Wild birds",
                        Children =
                        [
                            new TreeNodeViewModel { Value = "pigeon", Label = "Pigeon" },
                            new TreeNodeViewModel { Value = "gull", Label = "Gull" }
                        ]
                    }
                ]
            },
            new TreeNodeViewModel
            {
                Value = "aquatic",
                Label = "Aquatic animals (placeholder)",
                Children =
                [
                    new TreeNodeViewModel
                    {
                        Value = "finfish",
                        Label = "Finfish",
                        Children =
                        [
                            new TreeNodeViewModel { Value = "atlantic-salmon", Label = "Atlantic salmon" },
                            new TreeNodeViewModel { Value = "rainbow-trout", Label = "Rainbow trout" }
                        ]
                    },
                    new TreeNodeViewModel
                    {
                        Value = "molluscs",
                        Label = "Molluscs",
                        Children =
                        [
                            new TreeNodeViewModel { Value = "pacific-oyster", Label = "Pacific oyster" },
                            new TreeNodeViewModel { Value = "blue-mussel", Label = "Blue mussel" }
                        ]
                    }
                ]
            }
        ]
    };

    public void OnGet()
    {
    }
}
