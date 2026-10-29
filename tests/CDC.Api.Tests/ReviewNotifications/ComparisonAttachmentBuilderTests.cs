using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.ReviewNotifications.Dtos;
using CDC.Api.Features.ReviewNotifications.Interfaces;
using CDC.Api.Infrastructure.Pdf;
using CDC.Common.Comparison;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ComparisonAttachmentBuilderTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid DraftId = Guid.NewGuid();
    private static readonly Guid PublishedId = Guid.NewGuid();

    private readonly Mock<IProfileReportRepository> reports = new();
    private readonly Mock<IProfileManagementRepository> profiles = new();
    private readonly Mock<IComparisonDataSource> data = new();
    private readonly Mock<IPdfRenderer> pdf = new();
    private string? renderedHtml;

    public ComparisonAttachmentBuilderTests()
    {
        reports.Setup(r => r.GetProfileVersionHeaderAsync(DraftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionHeader { Id = DraftId, ProfileId = ProfileId, Title = "Anthrax", VersionMajor = 2, VersionMinor = 1 });
        reports.Setup(r => r.GetProfileVersionHeaderAsync(PublishedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionHeader { Id = PublishedId, ProfileId = ProfileId, Title = "Anthrax", VersionMajor = 2, VersionMinor = 0, IsPublished = true });
        profiles.Setup(p => p.GetProfileAttributesAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(Profile(PublishedId));

        var sectionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        data.Setup(d => d.GetMetadataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto
            {
                Id = sectionId,
                Name = "Intro",
                SectionNumber = 1,
                Questions = [new ProfileQuestionMetadataDto
                {
                    Id = questionId,
                    ShortName = "Q",
                    QuestionNumber = 1,
                    Fields = [new ProfileFieldMetadataDto { FieldNumber = 1, Name = "Notes", DataTypeName = "Text" }]
                }]
            }]
        });
        data.Setup(d => d.GetAnswersAsync(PublishedId, sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Answers(questionId, "before"));
        data.Setup(d => d.GetAnswersAsync(DraftId, sectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Answers(questionId, "after"));
        pdf.Setup(p => p.RenderLandscapeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((html, _) => renderedHtml = html)
            .ReturnsAsync([1, 2, 3]);
    }

    private static Profile Profile(Guid publishedId) => new()
    {
        Id = ProfileId,
        Title = "Anthrax",
        ScenarioTitle = string.Empty,
        ParentId = Guid.Empty,
        ParentTitle = string.Empty,
        CurrentDraftProfileVersionId = DraftId,
        CurrentPublishedProfileVersionId = publishedId,
        CurrentPublicVersionId = Guid.Empty,
        HasPublicScenarios = false,
        ProfileStatusId = Guid.Empty,
        LastUpdated = [],
        AffectedSpecies = []
    };

    private static ProfileSectionAnswersDto Answers(Guid questionId, string text) => new()
    {
        QuestionNames = [new ProfileQuestionNameDto { Id = questionId, Name = "Is it here?" }],
        FieldValues = [new ProfileFieldValueDto { QuestionId = questionId, FieldNumber = 1, TextValue = text }]
    };

    private ComparisonAttachmentBuilder CreateBuilder(string attachmentTemplateId = "attachment-template") => new(
        reports.Object,
        profiles.Object,
        data.Object,
        pdf.Object,
        Options.Create(new NotifyOptions { AttachmentTemplateId = attachmentTemplateId }),
        NullLogger<ComparisonAttachmentBuilder>.Instance);

    private static ReviewDueUser User(params Guid[] versionIds) => new(
        Guid.NewGuid(),
        "Ann",
        "ann@example.test",
        [.. versionIds.Select(id => new ReviewDueProfile(ProfileId, id, "Anthrax", null, "2.1", []))]);

    [Fact]
    public async Task BuildAsync_RendersThePublishedToDraftComparison()
    {
        var attachment = await CreateBuilder().BuildAsync(User(DraftId), CancellationToken.None);

        attachment!.Content.Should().Equal(1, 2, 3);
        attachment.FileName.Should().Be("Anthrax 2.0 - Anthrax 2.1 Comparison.pdf");
        renderedHtml.Should().Contain("Is it here?").And.Contain("before").And.Contain("after");
    }

    [Fact]
    public async Task BuildAsync_NamesOneFileForSeveralProfiles()
    {
        var attachment = await CreateBuilder().BuildAsync(User(DraftId, DraftId), CancellationToken.None);

        attachment!.FileName.Should().Be("Profile version comparisons.pdf");
    }

    [Fact]
    public async Task BuildAsync_ReturnsNull_WhenAttachmentsAreNotConfigured()
    {
        var attachment = await CreateBuilder(attachmentTemplateId: " ").BuildAsync(User(DraftId), CancellationToken.None);

        attachment.Should().BeNull();
        pdf.Verify(p => p.RenderLandscapeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BuildAsync_ReturnsNull_WhenTheProfileHasNoPublishedVersion()
    {
        profiles.Setup(p => p.GetProfileAttributesAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(Profile(Guid.Empty));

        (await CreateBuilder().BuildAsync(User(DraftId), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task BuildAsync_ReturnsNull_WhenTheProfileOrVersionsCannotBeFound()
    {
        (await CreateBuilder().BuildAsync(User(Guid.NewGuid()), CancellationToken.None)).Should().BeNull();

        profiles.Setup(p => p.GetProfileAttributesAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync((Profile?)null);
        (await CreateBuilder().BuildAsync(User(DraftId), CancellationToken.None)).Should().BeNull();

        profiles.Setup(p => p.GetProfileAttributesAsync(ProfileId, It.IsAny<CancellationToken>())).ReturnsAsync(Profile(Guid.NewGuid()));
        (await CreateBuilder().BuildAsync(User(DraftId), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task BuildAsync_ReturnsNull_WhenThePdfIsOverTheNotifyLimit()
    {
        pdf.Setup(p => p.RenderLandscapeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[ComparisonAttachmentBuilder.MaxFileBytes + 1]);

        (await CreateBuilder().BuildAsync(User(DraftId), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task BuildAsync_StripsMarkupAndInvalidCharactersFromTheFileName()
    {
        reports.Setup(r => r.GetProfileVersionHeaderAsync(PublishedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionHeader { Id = PublishedId, ProfileId = ProfileId, Title = "<b>Anthrax/x</b>", VersionMajor = 2, VersionMinor = 0 });

        var attachment = await CreateBuilder().BuildAsync(User(DraftId), CancellationToken.None);

        attachment!.FileName.Should().Be("Anthrax_x 2.0 - Anthrax 2.1 Comparison.pdf");
    }
}
