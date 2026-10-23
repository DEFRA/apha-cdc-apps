using System.Globalization;
using CDC.Web.Models;

namespace CDC.Web.Tests.Models;

public class PaginationViewModelTests
{
    private const string Ellipsis = "…";

    // The sequences the GOV.UK Design System gives as examples for a list of 100 pages.
    [Theory]
    [InlineData(1, "1 2 … 100")]
    [InlineData(2, "1 2 3 … 100")]
    [InlineData(3, "1 2 3 4 … 100")]
    [InlineData(4, "1 2 3 4 5 … 100")]
    [InlineData(5, "1 … 4 5 6 … 100")]
    [InlineData(50, "1 … 49 50 51 … 100")]
    [InlineData(97, "1 … 96 97 98 99 100")]
    [InlineData(98, "1 … 97 98 99 100")]
    [InlineData(99, "1 … 98 99 100")]
    [InlineData(100, "1 … 99 100")]
    public void BuildItems_ListsTheFirstLastAndCurrentPagesAndTheirNeighbours_WithEllipsesForTheRest(int currentPage, string expected) =>
        Assert.Equal(expected, Describe(PaginationViewModel.BuildItems(currentPage, 100)));

    [Theory]
    [InlineData(1, 1, "1")]
    [InlineData(1, 2, "1 2")]
    [InlineData(2, 2, "1 2")]
    [InlineData(3, 5, "1 2 3 4 5")]
    [InlineData(1, 4, "1 2 3 4")]
    [InlineData(1, 7, "1 2 … 7")]
    public void BuildItems_ListsEveryPage_WhenThereAreFew(int currentPage, int totalPages, string expected) =>
        Assert.Equal(expected, Describe(PaginationViewModel.BuildItems(currentPage, totalPages)));

    // An ellipsis for one page would stand for no more than it takes to list it.
    [Theory]
    [InlineData(3, 6, "1 2 3 4 5 6")]
    [InlineData(4, 100, "1 2 3 4 5 … 100")]
    [InlineData(97, 100, "1 … 96 97 98 99 100")]
    public void BuildItems_ListsASinglePageLeftOut_InsteadOfAnEllipsis(int currentPage, int totalPages, string expected) =>
        Assert.Equal(expected, Describe(PaginationViewModel.BuildItems(currentPage, totalPages)));

    [Theory]
    [InlineData(0, 3, "1 2 3")]
    [InlineData(-4, 3, "1 2 3")]
    [InlineData(99, 3, "1 2 3")]
    [InlineData(99, 100, "1 … 98 99 100")]
    public void BuildItems_TreatsAnOutOfRangeCurrentPageAsTheNearestPage(int currentPage, int totalPages, string expected) =>
        Assert.Equal(expected, Describe(PaginationViewModel.BuildItems(currentPage, totalPages)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildItems_ListsNothing_WhenThereAreNoPages(int totalPages) =>
        Assert.Empty(PaginationViewModel.BuildItems(1, totalPages));

    [Fact]
    public void BuildItems_MarksOnlyTheEllipsesAsEllipses()
    {
        var items = PaginationViewModel.BuildItems(5, 100);

        Assert.Equal([false, true, false, false, false, true, false], items.Select(item => item.IsEllipsis));
        Assert.All(items.Where(item => item.IsEllipsis), item => Assert.Null(item.Number));
    }

    [Fact]
    public void Items_ListsThePagesAroundTheCurrentPage()
    {
        var viewModel = new PaginationViewModel { CurrentPage = 5, TotalPages = 100, PageUrl = page => $"/list?page={page}" };

        Assert.Equal("1 … 4 5 6 … 100", Describe(viewModel.Items));
    }

    [Fact]
    public void LandmarkLabel_DefaultsToPagination_AndPageDataIsOffByDefault()
    {
        var viewModel = new PaginationViewModel { CurrentPage = 1, TotalPages = 2, PageUrl = _ => null };

        Assert.Equal("Pagination", viewModel.LandmarkLabel);
        Assert.False(viewModel.AddPageData);
    }

    [Theory]
    [InlineData(1, 4, "General reports (page 1 of 4)")]
    [InlineData(4, 4, "General reports (page 4 of 4)")]
    public void TitleWithPage_ShowsThePageNumber_WhenThereIsMoreThanOnePage(int currentPage, int totalPages, string expected) =>
        Assert.Equal(expected, PaginationViewModel.TitleWithPage("General reports", currentPage, totalPages));

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void TitleWithPage_LeavesTheTitleAlone_WhenThereIsOnePage(int totalPages) =>
        Assert.Equal("General reports", PaginationViewModel.TitleWithPage("General reports", 1, totalPages));

    private static string Describe(IEnumerable<PaginationItem> items) =>
        string.Join(' ', items.Select(item => item.Number?.ToString(CultureInfo.InvariantCulture) ?? Ellipsis));
}
