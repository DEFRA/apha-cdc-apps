using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Queries;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportValidatorTests
{
    [Fact]
    public void GetProfileVersionReportsQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetProfileVersionReportsQueryValidator().Validate(new GetProfileVersionReportsQuery(Guid.Empty, false));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileReportDataQueryValidator_EmptyReportId_ShouldFail()
    {
        var result = new GetProfileReportDataQueryValidator().Validate(new GetProfileReportDataQuery(ProfileReportTestData.ProfileVersionId, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetContributionsReportQueryValidator_EmptyProfileId_ShouldFail()
    {
        var result = new GetContributionsReportQueryValidator().Validate(new GetContributionsReportQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfilePrintVersionQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetProfilePrintVersionQueryValidator().Validate(new GetProfilePrintVersionQuery(Guid.Empty, ProfileReportTestData.ProfileSectionId));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionComparisonReportQueryValidator_EmptySourceVersionId_ShouldFail()
    {
        var result = new GetProfileVersionComparisonReportQueryValidator().Validate(
            new GetProfileVersionComparisonReportQuery(Guid.Empty, ProfileReportTestData.ProfileVersionId));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionComparisonReportQueryValidator_EmptyTargetVersionId_ShouldFail()
    {
        var result = new GetProfileVersionComparisonReportQueryValidator().Validate(
            new GetProfileVersionComparisonReportQuery(ProfileReportTestData.ProfileVersionId, Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionComparisonReportQueryValidator_SameSourceAndTargetVersion_ShouldFail()
    {
        var result = new GetProfileVersionComparisonReportQueryValidator().Validate(
            new GetProfileVersionComparisonReportQuery(ProfileReportTestData.ProfileVersionId, ProfileReportTestData.ProfileVersionId));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionComparisonReportQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileVersionComparisonReportQueryValidator().Validate(
            new GetProfileVersionComparisonReportQuery(ProfileReportTestData.ProfileVersionId, ProfileReportTestData.ProfileReportId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileVersionBespokeReportQueryValidator_NoSelections_ShouldFail()
    {
        var query = new GetProfileVersionBespokeReportQuery(ProfileReportTestData.ProfileVersionId, [], [], [], "Template");

        var result = new GetProfileVersionBespokeReportQueryValidator().Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileVersionBespokeReportQueryValidator_ValidRequest_ShouldPass()
    {
        var query = new GetProfileVersionBespokeReportQuery(ProfileReportTestData.ProfileVersionId, ["Section1"], [], [], "Template");

        var result = new GetProfileVersionBespokeReportQueryValidator().Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetSummaryPrioritisationReportQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetSummaryPrioritisationReportQueryValidator().Validate(new GetSummaryPrioritisationReportQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetSummaryProfileReportQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetSummaryProfileReportQueryValidator().Validate(new GetSummaryProfileReportQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileRankingReportQueryValidator_ValidRequest_ShouldPass()
    {
        var result = new GetProfileRankingReportQueryValidator().Validate(new GetProfileRankingReportQuery(ProfileRankingReportType.All, "Filter"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateProfileReportCommandValidator_EmptyReportData_ShouldFail()
    {
        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = []
        };

        var result = new CreateProfileReportCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateProfileReportCommandValidator_ValidRequest_ShouldPass()
    {
        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = ProfileReportTestData.ReportBytes
        };

        var result = new CreateProfileReportCommandValidator().Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
