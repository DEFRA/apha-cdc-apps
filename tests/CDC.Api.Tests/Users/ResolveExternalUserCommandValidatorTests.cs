using CDC.Api.Features.Users.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Users;

public class ResolveExternalUserCommandValidatorTests
{
    private static ResolveExternalUserCommand ValidCommand() => new()
    {
        SsoUserIdExt = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Email = "user@example.com",
        FirstName = "Jane",
        LastName = "External",
        Organisation = "ACME Ltd"
    };

    [Fact]
    public void Validator_Passes_ForAValidCommand()
    {
        var result = new ResolveExternalUserCommandValidator().Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_RequiresSsoUserIdExt()
    {
        var command = ValidCommand() with { SsoUserIdExt = Guid.Empty };

        var result = new ResolveExternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.SsoUserIdExt));
    }

    [Fact]
    public void Validator_RequiresEmail()
    {
        var command = ValidCommand() with { Email = string.Empty };

        var result = new ResolveExternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.Email));
    }

    [Fact]
    public void Validator_RequiresAValidEmailFormat()
    {
        var command = ValidCommand() with { Email = "not-an-email" };

        var result = new ResolveExternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.Email));
    }

    [Fact]
    public void Validator_RejectsEmailLongerThanFiftyCharacters()
    {
        var longLocalPart = new string('a', 45);
        var command = ValidCommand() with { Email = $"{longLocalPart}@example.com" };

        var result = new ResolveExternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.Email));
    }

    [Fact]
    public void Validator_RejectsNamesAndOrganisationLongerThanOneHundredCharacters()
    {
        var tooLong = new string('a', 101);
        var command = ValidCommand() with { FirstName = tooLong, LastName = tooLong, Organisation = tooLong };

        var result = new ResolveExternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.FirstName));
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.LastName));
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveExternalUserCommand.Organisation));
    }
}
