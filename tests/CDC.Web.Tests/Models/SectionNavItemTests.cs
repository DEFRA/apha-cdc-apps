using CDC.Web.Models;

namespace CDC.Web.Tests.Models;

public class SectionNavItemTests
{
    [Fact]
    public void SectionNavItem_DefaultsIsCurrentToFalse()
    {
        var item = new SectionNavItem(1, "Section 1", "/section-1");

        Assert.False(item.IsCurrent);
        Assert.Equal(1, item.Number);
        Assert.Equal("Section 1", item.Title);
        Assert.Equal("/section-1", item.Url);
    }

    [Fact]
    public void SectionNavItem_SetsIsCurrent_WhenExplicitlyTrue()
    {
        var item = new SectionNavItem(2, "Section 2", null, IsCurrent: true);

        Assert.True(item.IsCurrent);
        Assert.Null(item.Url);
    }

    [Fact]
    public void SectionSubnavViewModel_HoldsAriaLabelAndItems()
    {
        IReadOnlyList<SectionNavItem> items = [new SectionNavItem(1, "Section 1", "/section-1")];

        var viewModel = new SectionSubnavViewModel("Section navigation", items);

        Assert.Equal("Section navigation", viewModel.AriaLabel);
        Assert.Same(items, viewModel.Items);
    }

    [Fact]
    public void SectionPaginationViewModel_HoldsPreviousAndNext()
    {
        var previous = new SectionNavItem(1, "Section 1", "/section-1");
        var next = new SectionNavItem(3, "Section 3", "/section-3");

        var viewModel = new SectionPaginationViewModel(previous, next);

        Assert.Same(previous, viewModel.Previous);
        Assert.Same(next, viewModel.Next);
    }
}
