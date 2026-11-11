using CDC.Api.Features.Users.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.Users;

public class LookUpUserByEmailCommandValidatorTests
{
    private static LookUpUserByEmailCommand ValidCommand() => new() { EmailAddress = "user@example.com" };

    [Fact]
    public void Validator_Passes_ForAValidCommand()
    {
        var result = new LookUpUserByEmailCommandValidator().Validate(ValidCommand());

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_RequiresAnEmailAddress(string emailAddress)
    {
        var result = new LookUpUserByEmailCommandValidator().Validate(ValidCommand() with { EmailAddress = emailAddress });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(LookUpUserByEmailCommand.EmailAddress));
    }

    [Fact]
    public void Validator_RequiresAValidEmailFormat()
    {
        var result = new LookUpUserByEmailCommandValidator().Validate(ValidCommand() with { EmailAddress = "not-an-email" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(LookUpUserByEmailCommand.EmailAddress));
    }

    [Fact]
    public void Validator_RejectsAnEmailAddressLongerThanFiftyCharacters()
    {
        var longLocalPart = new string('a', 45);

        var result = new LookUpUserByEmailCommandValidator().Validate(ValidCommand() with { EmailAddress = $"{longLocalPart}@example.com" });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.PropertyName == nameof(LookUpUserByEmailCommand.EmailAddress));
    }
}
