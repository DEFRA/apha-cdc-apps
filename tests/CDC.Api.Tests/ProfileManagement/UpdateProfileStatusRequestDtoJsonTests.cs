using System.Text.Json;
using CDC.Api.Features.ProfileManagement.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileManagement;

// The profile comes from the route, so the body the web app sends carries only the status.
public class UpdateProfileStatusRequestDtoJsonTests
{
    [Fact]
    public void JsonDeserialization_AcceptsABodyThatCarriesOnlyTheStatus()
    {
        var json = $$"""{ "profileStatusId": "{{ProfileManagementTestData.ProfileStatusId}}" }""";

        var request = JsonSerializer.Deserialize<UpdateProfileStatusRequestDto>(json, JsonSerializerOptions.Web);

        request.Should().NotBeNull();
        request!.ProfileStatusId.Should().Be(ProfileManagementTestData.ProfileStatusId);
    }

    [Fact]
    public void JsonDeserialization_RequiresTheStatus()
    {
        Action act = () => JsonSerializer.Deserialize<UpdateProfileStatusRequestDto>("{}", JsonSerializerOptions.Web);

        act.Should().Throw<JsonException>()
            .WithMessage("*ProfileStatusId*");
    }
}
