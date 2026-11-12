using CDC.Api.Domain.Entities;
using CDC.Api.Features.ExternalUsers;
using FluentAssertions;

namespace CDC.Api.Tests.ExternalUsers;

public class ExternalUserLoginInstructionsComposerTests
{
    private const string HomePage = "https://d2r2.example.test/";

    private static ExternalUserLoginRecipient User(string email = "jo@example.test") =>
        new(Guid.NewGuid(), "Jo Smith", email, LoginInstructionsSent: false);

    [Fact]
    public void Compose_UsesTheLoginInstructionsTemplate_AddressedToTheSavedAddress()
    {
        var message = ExternalUserLoginInstructionsComposer.Compose(User(" jo@example.test "), HomePage);

        message.Should().NotBeNull();
        message!.ToAddress.Should().Be("jo@example.test");
        message.TemplateId.Should().Be("8c79693e-ad6e-4bc1-9b86-0b656b55b7de");
    }

    [Fact]
    public void Compose_PassesTheNameAndTheAppUrl_BecauseTheTemplateHoldsTheTextAndSubject()
    {
        var message = ExternalUserLoginInstructionsComposer.Compose(User(), HomePage)!;

        message.Personalisation.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["name"] = "Jo Smith", ["AppUrl"] = "https://d2r2.example.test/" });
        message.Subject.Should().BeEmpty();
        message.Body.Should().BeEmpty();
    }

    [Fact]
    public void Compose_StripsMarkupFromTheName_SoItCannotInjectLinks()
    {
        var user = User() with { FullName = "Jo [click](https://evil.test) <b>Smith</b>" };

        var name = ExternalUserLoginInstructionsComposer.Compose(user, HomePage)!.Personalisation!["name"];

        name.Should().NotContainAny("[", "]", "<", ">");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<b></b>")]
    [InlineData(null)]
    public void Compose_GreetsByEmailAddress_WhenThereIsNoUsableName(string? fullName)
    {
        var user = User(" jo@example.test ") with { FullName = fullName! };

        ExternalUserLoginInstructionsComposer.Compose(user, HomePage)!.Personalisation!["name"].Should().Be("jo@example.test");
    }

    [Fact]
    public void Compose_NormalisesTheAppUrl() =>
        ExternalUserLoginInstructionsComposer.Compose(User(), "https://d2r2.example.test")!.Personalisation!["AppUrl"]
            .Should().Be("https://d2r2.example.test/");

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("/relative")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.test/")]
    public void Compose_ReturnsNull_WhenThereIsNoValidWebAddressToLinkTo(string homePageUrl) =>
        ExternalUserLoginInstructionsComposer.Compose(User(), homePageUrl).Should().BeNull();

    [Fact]
    public void Compose_Throws_WhenTheUserIsMissing()
    {
        var act = () => ExternalUserLoginInstructionsComposer.Compose(null!, HomePage);

        act.Should().Throw<ArgumentNullException>();
    }
}
