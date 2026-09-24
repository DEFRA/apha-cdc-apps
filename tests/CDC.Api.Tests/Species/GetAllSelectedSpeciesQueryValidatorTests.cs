using CDC.Api.Features.Species.Queries;
using FluentAssertions;
using FluentValidation;

namespace CDC.Api.Tests.Species;

public class GetAllSelectedSpeciesQueryValidatorTests
{
    private readonly GetAllSelectedSpeciesQueryValidator validator = new();

    [Fact]
    public void Validate_WithValidDiseaseName_ShouldPass()
    {
        var query = new GetAllSelectedSpeciesQuery("Bovine tuberculosis");

        var result = validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyDiseaseName_ShouldFail()
    {
        var query = new GetAllSelectedSpeciesQuery(string.Empty);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A disease name is required.");
    }

    [Fact]
    public void Validate_WithTooLongDiseaseName_ShouldFail()
    {
        var longName = new string('x', 501);
        var query = new GetAllSelectedSpeciesQuery(longName);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A disease name must be 500 characters or fewer.");
    }

    [Fact]
    public void Validate_WithMaxLengthDiseaseName_ShouldPass()
    {
        var maxName = new string('x', 500);
        var query = new GetAllSelectedSpeciesQuery(maxName);

        var result = validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}
