using CDC.Web.Models;
using CDC.Web.Pages.HelpSupport;

namespace CDC.Web.Tests.Pages;

public class HelpUsingD2R2ModelTests
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly IReadOnlyList<StaticReportVersionDto> Manuals =
    [
        new StaticReportVersionDto
        {
            Id = VersionId,
            StaticReportId = StaticReportId,
            Title = "Help using D2R2 guidance",
            VersionMajor = 1,
            EffectiveDateFrom = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
            IsCurrent = true,
            IsUserManual = true,
            IsPublic = false,
            FileSize = 4096
        }
    ];

    [Fact]
    public async Task OnGetAsync_LoadsCurrentManuals()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(Manuals));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Single(pageModel.Documents);
        Assert.False(pageModel.StatusIsError);
    }

    [Fact]
    public async Task OnGetAsync_SetsErrorStatus_WhenApiCallFails()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(throwOnGetCurrent: new HttpRequestException("connection refused")));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Empty(pageModel.Documents);
        Assert.True(pageModel.StatusIsError);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.StatusMessage));
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsFile_WhenVersionExists()
    {
        var data = new StaticReportDataDto { PdfData = [1, 2, 3], Title = "Help using D2R2 guidance" };
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(Manuals, data: data));

        var result = await pageModel.OnGetDownloadAsync(VersionId, CancellationToken.None);

        var fileResult = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal(data.PdfData, fileResult.FileContents);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsNotFound_WhenVersionDoesNotExist()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(Manuals));

        var result = await pageModel.OnGetDownloadAsync(VersionId, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetDownloadAsync_ReturnsBadGateway_WhenApiCallFails()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(throwOnGetData: new HttpRequestException("connection refused")));

        var result = await pageModel.OnGetDownloadAsync(VersionId, CancellationToken.None);

        var statusResult = Assert.IsType<Microsoft.AspNetCore.Mvc.StatusCodeResult>(result);
        Assert.Equal(502, statusResult.StatusCode);
    }

    [Fact]
    public async Task OnPostUploadAsync_RejectsNonPdfFiles()
    {
        var fakeService = new FakeStaticReportsApiService(Manuals);
        var pageModel = CreatePageModel(fakeService);
        var file = CreateFormFile("manual.docx", "application/msword");

        await pageModel.OnPostUploadAsync(file, CancellationToken.None);

        Assert.True(pageModel.StatusIsError);
        Assert.Equal("Failed to upload the user manual: The uploaded user manual must be a PDF.", pageModel.StatusMessage);
        Assert.True(pageModel.ShowUploadPanel);
        Assert.Null(fakeService.UploadedRequest);
    }

    [Fact]
    public async Task OnPostUploadAsync_RejectsMissingFile()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(Manuals));

        await pageModel.OnPostUploadAsync(null, CancellationToken.None);

        Assert.True(pageModel.StatusIsError);
        Assert.True(pageModel.ShowUploadPanel);
    }

    [Fact]
    public async Task OnPostUploadAsync_UploadsPdf_AndReloadsDocuments()
    {
        var fakeService = new FakeStaticReportsApiService(Manuals);
        var pageModel = CreatePageModel(fakeService);
        var file = CreateFormFile("Help using D2R2 guidance.pdf", "application/pdf");

        var result = await pageModel.OnPostUploadAsync(file, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.False(pageModel.StatusIsError);
        Assert.False(pageModel.ShowUploadPanel);
        Assert.Equal("Help using D2R2 guidance", fakeService.UploadedRequest?.Title);
        Assert.True(fakeService.UploadedRequest?.IsUserManual);
        Assert.Single(pageModel.Documents);
    }

    [Fact]
    public async Task OnPostUploadAsync_SetsErrorStatus_WhenApiCallFails()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(throwOnUpload: new HttpRequestException("connection refused")));
        var file = CreateFormFile("Help using D2R2 guidance.pdf", "application/pdf");

        await pageModel.OnPostUploadAsync(file, CancellationToken.None);

        Assert.True(pageModel.StatusIsError);
        Assert.True(pageModel.ShowUploadPanel);
        Assert.StartsWith("Failed to upload the user manual: ", pageModel.StatusMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_DeletesDocument_AndReloadsList()
    {
        var fakeService = new FakeStaticReportsApiService(Manuals);
        var pageModel = CreatePageModel(fakeService);

        var result = await pageModel.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.False(pageModel.StatusIsError);
        Assert.Equal(VersionId, fakeService.DeletedStaticReportVersionId);
    }

    [Fact]
    public async Task OnPostDeleteAsync_SetsErrorStatus_WhenDeleteFails()
    {
        var deleteResult = new StaticReportUpdateResult { Outcome = StaticReportUpdateOutcome.Conflict, ErrorMessage = "not current" };
        var fakeService = new FakeStaticReportsApiService(Manuals, deleteResult: deleteResult);
        var pageModel = CreatePageModel(fakeService);

        await pageModel.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.True(pageModel.StatusIsError);
        Assert.Equal("not current", pageModel.StatusMessage);
    }

    [Fact]
    public async Task OnPostDeleteAsync_SetsErrorStatus_WhenApiCallFails()
    {
        var pageModel = CreatePageModel(new FakeStaticReportsApiService(throwOnDelete: new HttpRequestException("connection refused")));

        await pageModel.OnPostDeleteAsync(VersionId, CancellationToken.None);

        Assert.True(pageModel.StatusIsError);
    }

    private static HelpUsingD2R2Model CreatePageModel(FakeStaticReportsApiService service) =>
        new(service, new AlwaysEnabledLogger<HelpUsingD2R2Model>());

    private static Microsoft.AspNetCore.Http.FormFile CreateFormFile(string fileName, string contentType)
    {
        var bytes = "%PDF-1.4"u8.ToArray();
        var stream = new MemoryStream(bytes);

        return new Microsoft.AspNetCore.Http.FormFile(stream, 0, bytes.Length, "document", fileName)
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = contentType
        };
    }
}
