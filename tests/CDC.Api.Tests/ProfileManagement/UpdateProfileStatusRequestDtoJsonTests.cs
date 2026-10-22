using System.Text.Json;
using CDC.Api.Features.ProfileManagement.Commands;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileManagement;

// The profile comes from the route, so the body the web app sends carries only the status.
public class UpdateProfileStatusCommandJsonTests
{
    [Fact]
    public void JsonDeserialization_AcceptsABodyThatCarriesOnlyTheStatus()
    {
        var json = $$"""{ "profileStatusId": "{{ProfileManagementTestData.ProfileStatusId}}" }""";

        var command = JsonSerializer.Deserialize<UpdateProfileStatusCommand>(json, JsonSerializerOptions.Web);

        command.Should().NotBeNull();
        command!.ProfileStatusId.Should().Be(ProfileManagementTestData.ProfileStatusId);
        command.ProfileId.Should().BeEmpty();
    }

    [Fact]
    public void JsonDeserialization_RequiresTheStatus()
    {
        Action act = () => JsonSerializer.Deserialize<UpdateProfileStatusCommand>("{}", JsonSerializerOptions.Web);

        act.Should().Throw<JsonException>()
            .WithMessage("*ProfileStatusId*");
    }
}
