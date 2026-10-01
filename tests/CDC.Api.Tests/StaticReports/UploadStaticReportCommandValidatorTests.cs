using CDC.Api.Features.StaticReports.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.StaticReports;

public class UploadStaticReportCommandValidatorTests
{
    [Fact]
    public void ValidCommand_Passes()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand("D2R2 Quality Statement", [1, 2, 3], true, false));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyTitle_Fails()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand(string.Empty, [1, 2, 3], false, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyPdfData_Fails()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand("D2R2 Quality Statement", [], false, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UserManualMarkedPublic_Fails()
    {
        var result = new UploadStaticReportCommandValidator().Validate(
            new UploadStaticReportCommand("D2R2 Quality Statement", [1, 2, 3], true, true));

        result.IsValid.Should().BeFalse();
    }
}
