using CDC.Api.Features.Species.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Species;

public class InactivateSpeciesCommandValidatorTests
{
    private readonly InactivateSpeciesCommandValidator validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = validator.Validate(SpeciesTestData.InactivateSpeciesCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingSpeciesId_ShouldFail()
    {
        var command = SpeciesTestData.InactivateSpeciesCommand() with { SpeciesId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.SpeciesId));
    }

    [Fact]
    public void MissingUserId_ShouldFail()
    {
        var command = SpeciesTestData.InactivateSpeciesCommand() with { UserId = Guid.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.UserId));
    }

    [Fact]
    public void MissingReason_ShouldFail()
    {
        var command = SpeciesTestData.InactivateSpeciesCommand() with { Reason = string.Empty };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "You need to provide a reason for this change.");
    }

    [Fact]
    public void ReasonTooLong_ShouldFail()
    {
        var command = SpeciesTestData.InactivateSpeciesCommand() with { Reason = new string('a', 256) };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "The reason for change must be no longer than 255 characters.");
    }

    [Fact]
    public void WrongLastUpdatedLength_ShouldFail()
    {
        var command = SpeciesTestData.InactivateSpeciesCommand() with { LastUpdated = [1, 2, 3] };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.LastUpdated));
    }
}
