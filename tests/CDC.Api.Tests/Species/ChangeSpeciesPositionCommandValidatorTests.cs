using CDC.Api.Features.Species.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Species;

public class ChangeSpeciesPositionCommandValidatorTests
{
    private readonly ChangeSpeciesPositionCommandValidator validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var result = validator.Validate(new ChangeSpeciesPositionCommand(SpeciesTestData.SpeciesId, IsMovingUp: true, SpeciesTestData.AuditUserId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MissingSpeciesId_ShouldFail()
    {
        var command = new ChangeSpeciesPositionCommand(Guid.Empty, IsMovingUp: true, SpeciesTestData.AuditUserId);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(command.SpeciesId));
    }
}
