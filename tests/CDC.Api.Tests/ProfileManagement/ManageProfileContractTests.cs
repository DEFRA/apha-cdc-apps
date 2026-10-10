using CDC.Api.Features.ProfileManagement.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileManagement;

public class ManageProfileContractTests
{
    private static readonly Guid CurrentProfileVersionId = Guid.NewGuid();

    [Fact]
    public void CurrentVersionDisplay_FormatsPublishedVersion_WithoutAValidToDate()
    {
        var response = new GetManageProfileResponse
        {
            CurrentProfileVersionId = CurrentProfileVersionId,
            CurrentVersionNumber = "13.0",
            CurrentVersionIsPublished = true,
            CurrentVersionValidFrom = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
        };

        response.CurrentVersionDisplay.Should().Be("Version 13.0 (Published) Valid from 11/09/2026");
    }

    [Fact]
    public void CurrentVersionDisplay_FormatsDraftVersion_WithAValidToDate()
    {
        var response = new GetManageProfileResponse
        {
            CurrentProfileVersionId = CurrentProfileVersionId,
            CurrentVersionNumber = "12.2",
            CurrentVersionIsPublished = false,
            CurrentVersionValidFrom = new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc),
            CurrentVersionValidTo = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
        };

        response.CurrentVersionDisplay.Should().Be("Version 12.2 (Draft) Valid from 29/07/2026 to 11/09/2026");
    }

    [Fact]
    public void CurrentVersionDisplay_IsNull_WhenTheProfileHasNoCurrentVersion()
    {
        var response = new GetManageProfileResponse
        {
            CurrentProfileVersionId = Guid.Empty,
            CurrentVersionNumber = string.Empty
        };

        response.CurrentVersionDisplay.Should().BeNull();
    }
}
