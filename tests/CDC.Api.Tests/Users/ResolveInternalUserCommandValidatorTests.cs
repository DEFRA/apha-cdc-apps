using CDC.Api.Features.Users.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Users;

public class ResolveInternalUserCommandValidatorTests
{
    private static ResolveInternalUserCommand ValidCommand() => new()
    {
        SsoUserIdInt = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        UserName = @"DEFRA\jdoe",
        FullName = "Jane Internal"
    };

    [Fact]
    public void Validator_Passes_ForAValidCommand()
    {
        var result = new ResolveInternalUserCommandValidator().Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_RequiresSsoUserIdInt()
    {
        var command = ValidCommand() with { SsoUserIdInt = Guid.Empty };

        var result = new ResolveInternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveInternalUserCommand.SsoUserIdInt));
    }

    [Fact]
    public void Validator_RequiresUserName()
    {
        var command = ValidCommand() with { UserName = string.Empty };

        var result = new ResolveInternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveInternalUserCommand.UserName));
    }

    [Fact]
    public void Validator_RejectsUserNameLongerThanFiftyCharacters()
    {
        var command = ValidCommand() with { UserName = new string('a', 51) };

        var result = new ResolveInternalUserCommandValidator().Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(ResolveInternalUserCommand.UserName));
    }
}
