using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileContributors.Mapping;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileContributors;

public class ProfileContributorsMappingsTests
{
    [Fact]
    public void ToDto_MapsEveryFieldOfAContributor()
    {
        var contributor = new Contributor
        {
            Id = Guid.NewGuid(),
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            Role = "Technical author",
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        var dto = contributor.ToDto();

        dto.Id.Should().Be(contributor.Id);
        dto.UserName.Should().Be(contributor.UserName);
        dto.FullName.Should().Be(contributor.FullName);
        dto.Organisation.Should().Be(contributor.Organisation);
        dto.Role.Should().Be(contributor.Role);
        dto.LastUpdated.Should().Equal(contributor.LastUpdated);
    }

    [Fact]
    public void ToDto_MapsEveryFieldOfAContributorEdit()
    {
        var sectionId = Guid.NewGuid();
        var contributor = new ContributorEdit
        {
            Id = Guid.NewGuid(),
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            RoleId = Guid.NewGuid(),
            IsSsoUser = true,
            SectionPermissionIds = [sectionId],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };

        var dto = contributor.ToDto();

        dto.Id.Should().Be(contributor.Id);
        dto.UserName.Should().Be(contributor.UserName);
        dto.FullName.Should().Be(contributor.FullName);
        dto.Organisation.Should().Be(contributor.Organisation);
        dto.RoleId.Should().Be(contributor.RoleId);
        dto.IsSsoUser.Should().BeTrue();
        dto.SectionPermissionIds.Should().BeEquivalentTo([sectionId]);
        dto.LastUpdated.Should().Equal(contributor.LastUpdated);
    }

    [Fact]
    public void ToDto_MapsEveryFieldOfAProfileUserRole()
    {
        var role = new ProfileUserRole { Id = Guid.NewGuid(), Name = "Technical author", IsContributor = true };

        var dto = role.ToDto();

        dto.Id.Should().Be(role.Id);
        dto.Name.Should().Be(role.Name);
        dto.IsContributor.Should().BeTrue();
    }
}
