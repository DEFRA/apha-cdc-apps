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
    public void GetDiseaseReviewsReportQueryValidator_AcceptsAValidRangeAndAuthor()
    {
        var result = new GetDiseaseReviewsReportQueryValidator()
            .Validate(new GetDiseaseReviewsReportQuery(new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetDiseaseReviewsReportQueryValidator_AcceptsTheEdgesOfTheSupportedRange()
    {
        var result = new GetDiseaseReviewsReportQueryValidator()
            .Validate(new GetDiseaseReviewsReportQuery(new DateTime(1900, 1, 1), new DateTime(2079, 6, 6), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetDiseaseReviewsReportQueryValidator_EmptyAuthorId_ShouldFail()
    {
        var result = new GetDiseaseReviewsReportQueryValidator()
            .Validate(new GetDiseaseReviewsReportQuery(new DateTime(2027, 1, 1), new DateTime(2027, 12, 31), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(GetDiseaseReviewsReportQuery.AuthorId));
    }

    [Theory]
    [InlineData(1899, 12, 31)]
    [InlineData(2079, 6, 7)]
    public void GetDiseaseReviewsReportQueryValidator_DatesOutsideTheSupportedRange_ShouldFail(int year, int month, int day)
    {
        var outOfRange = new DateTime(year, month, day);
        var validator = new GetDiseaseReviewsReportQueryValidator();

        var fromResult = validator.Validate(new GetDiseaseReviewsReportQuery(outOfRange, new DateTime(2027, 12, 31), Guid.NewGuid()));
        var toResult = validator.Validate(new GetDiseaseReviewsReportQuery(new DateTime(2027, 1, 1), outOfRange, Guid.NewGuid()));

        fromResult.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(GetDiseaseReviewsReportQuery.FromDate));
        toResult.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(nameof(GetDiseaseReviewsReportQuery.ToDate));
    }

    [Fact]
    public void GetProfilePrioritisationScoresQueryValidator_ValidatesTheProfileVersionId()
    {
        var validator = new GetProfilePrioritisationScoresQueryValidator();

        validator.Validate(new GetProfilePrioritisationScoresQuery(Guid.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new GetProfilePrioritisationScoresQuery(ProfileReportTestData.ProfileVersionId)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileReportContentQueryValidator_ValidatesTheProfileVersionId()
    {
        var validator = new GetProfileReportContentQueryValidator();

        validator.Validate(new GetProfileReportContentQuery(Guid.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new GetProfileReportContentQuery(ProfileReportTestData.ProfileVersionId)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void PrioritiseProfileVersionCommandValidator_ValidatesTheProfileVersionId()
    {
        var validator = new PrioritiseProfileVersionCommandValidator();

        validator.Validate(new PrioritiseProfileVersionCommand(Guid.Empty)).IsValid.Should().BeFalse();
        validator.Validate(new PrioritiseProfileVersionCommand(ProfileReportTestData.ProfileVersionId)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetProfileReportsOverviewQueryValidator_EmptyProfileVersionId_ShouldFail()
    {
        var result = new GetProfileReportsOverviewQueryValidator().Validate(new GetProfileReportsOverviewQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetProfileReportsOverviewQueryValidator_ProfileVersionId_ShouldPass()
    {
        var result = new GetProfileReportsOverviewQueryValidator().Validate(new GetProfileReportsOverviewQuery(ProfileReportTestData.ProfileVersionId));

        result.IsValid.Should().BeTrue();
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
