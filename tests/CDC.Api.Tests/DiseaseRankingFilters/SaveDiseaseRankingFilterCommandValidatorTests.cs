using CDC.Api.Features.DiseaseRankingFilters.Commands;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.DiseaseRankingFilters;

public class SaveDiseaseRankingFilterCommandValidatorTests
{
    private static readonly Guid SpeciesId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly SaveDiseaseRankingFilterCommandValidator validator = new();

    private static SaveDiseaseRankingFilterRequestDto ValidRequest() => new()
    {
        DiseaseName = "Anthrax",
        Disease1 = "endemic diseases",
        Disease2 = "both",
        Disease3 = "both",
        Disease4 = "both",
        DiseaseTypes = ["bacteria", "virus"],
        SpeciesIds = [SpeciesId]
    };

    [Fact]
    public void Validate_PassesForAFullyPopulatedRequest()
    {
        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, ValidRequest()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FailsWhenDiseaseNameIsEmpty()
    {
        var request = ValidRequest() with { DiseaseName = string.Empty };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Enter a report title.");
    }

    [Fact]
    public void Validate_FailsWhenDiseaseNameExceedsMaximumLength()
    {
        var request = ValidRequest() with { DiseaseName = new string('a', 501) };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == "Request.DiseaseName");
    }

    [Fact]
    public void Validate_FailsWhenDisease1IsEmpty()
    {
        var request = ValidRequest() with { Disease1 = string.Empty };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Select endemic diseases, exotic diseases or both.");
    }

    [Fact]
    public void Validate_FailsWhenDisease2IsEmpty()
    {
        var request = ValidRequest() with { Disease2 = string.Empty };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Select zoonotic, non-zoonotic or both.");
    }

    [Fact]
    public void Validate_FailsWhenDisease3IsEmpty()
    {
        var request = ValidRequest() with { Disease3 = string.Empty };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Select notifiable, non-notifiable or both.");
    }

    [Fact]
    public void Validate_FailsWhenDisease4IsEmpty()
    {
        var request = ValidRequest() with { Disease4 = string.Empty };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Select infectious, non-infectious or both.");
    }

    [Fact]
    public void Validate_FailsWhenATransmissibleAgentTypeIsNotRecognised()
    {
        var request = ValidRequest() with { DiseaseTypes = ["not-a-real-type"] };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Transmissible agent type must be one of the recognised values.");
    }

    [Fact]
    public void Validate_FailsWhenNoSpeciesAreSelected()
    {
        var request = ValidRequest() with { SpeciesIds = [] };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "Select at least one species.");
    }

    [Fact]
    public void Validate_AllowsAnEmptyDiseaseTypesList()
    {
        var request = ValidRequest() with { DiseaseTypes = [] };

        var result = validator.Validate(new SaveDiseaseRankingFilterCommand(null, request));

        result.IsValid.Should().BeTrue();
    }
}
