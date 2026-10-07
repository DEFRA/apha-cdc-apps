using CDC.Api.Features.StaticReports.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.StaticReports;

public class StaticReportValidatorTests
{
    [Fact]
    public void UploadStaticReportCommandValidator_EmptyTitle_ShouldFail()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand(string.Empty, StaticReportTestData.PdfBytes, true, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UploadStaticReportCommandValidator_TitleTooLong_ShouldFail()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand(new string('a', 256), StaticReportTestData.PdfBytes, true, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UploadStaticReportCommandValidator_EmptyPdfData_ShouldFail()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand("Title", [], true, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UploadStaticReportCommandValidator_ValidCommand_ShouldPass()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand("Title", StaticReportTestData.PdfBytes, true, false));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DeleteStaticReportVersionCommandValidator_EmptyId_ShouldFail()
    {
        var result = new DeleteStaticReportVersionCommandValidator().Validate(new DeleteStaticReportVersionCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DeleteStaticReportVersionCommandValidator_ValidId_ShouldPass()
    {
        var result = new DeleteStaticReportVersionCommandValidator().Validate(
            new DeleteStaticReportVersionCommand(StaticReportTestData.VersionId));

        result.IsValid.Should().BeTrue();
    }
}
