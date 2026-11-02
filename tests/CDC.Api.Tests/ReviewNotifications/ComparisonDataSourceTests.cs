using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.ReviewNotifications;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ReviewNotifications;

public class ComparisonDataSourceTests
{
    private readonly Mock<IProfileSectionService> sections = new();
    private readonly Mock<IReferenceDataService> referenceData = new();
    private readonly Mock<ISpeciesService> species = new();

    private ComparisonDataSource CreateSource() => new(sections.Object, referenceData.Object, species.Object);

    [Fact]
    public async Task ReadsMetadataAndAnswersFromTheSectionService()
    {
        var metadata = new ProfileQuestionnaireMetadataDto();
        var answers = new ProfileSectionAnswersDto();
        var versionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        sections.Setup(s => s.GetProfileQuestionnaireMetadataAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metadata);
        sections.Setup(s => s.GetProfileSectionAnswersAsync(versionId, sectionId, It.IsAny<CancellationToken>())).ReturnsAsync(answers);

        (await CreateSource().GetMetadataAsync(CancellationToken.None)).Should().BeSameAs(metadata);
        (await CreateSource().GetAnswersAsync(versionId, sectionId, CancellationToken.None)).Should().BeSameAs(answers);
    }

    [Fact]
    public async Task KeysReferenceValuesAndSpeciesNamesById_KeepingTheFirstOfDuplicates()
    {
        var id = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        referenceData.Setup(r => r.GetReferenceValuesAsync(tableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReferenceValueDto { Id = id, Value = "First" }, new ReferenceValueDto { Id = id, Value = "Second" }]);
        species.Setup(s => s.GetAllSpeciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SpeciesDto { Id = id, Description = "Cattle" }, new SpeciesDto { Id = id, Description = "Other" }]);

        (await CreateSource().GetReferenceValuesAsync(tableId, CancellationToken.None)).Should().ContainSingle().Which.Value.Should().Be("First");
        (await CreateSource().GetSpeciesNamesAsync(CancellationToken.None)).Should().ContainSingle().Which.Value.Should().Be("Cattle");
    }
}
