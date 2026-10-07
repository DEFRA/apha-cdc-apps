using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileContributorsApiServiceTests
{
    [Fact]
    public async Task GetProfileContributorsAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "items": [
                {
                  "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                  "userName": "carrie.batten",
                  "fullName": "Carrie Batten",
                  "organisation": "Pirbright Institute",
                  "role": "Technical author"
                }
              ],
              "pageNumber": 1,
              "pageSize": 10,
              "totalRecords": 1
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.GetProfileContributorsAsync(Guid.NewGuid(), pageNumber: 1, pageSize: 10);

        var item = Assert.Single(result.Items);
        Assert.Equal("carrie.batten", item.UserName);
        Assert.Equal("Carrie Batten", item.FullName);
        Assert.Equal("Pirbright Institute", item.Organisation);
        Assert.Equal("Technical author", item.Role);
        Assert.Equal(1, result.TotalRecords);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetProfileContributorsAsync_ReturnsAnEmptyPage_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.GetProfileContributorsAsync(Guid.NewGuid(), pageNumber: 2, pageSize: 10);

        Assert.Empty(result.Items);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(0, result.TotalRecords);
    }

    [Fact]
    public async Task GetProfileContributorsAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetProfileContributorsAsync(Guid.NewGuid(), pageNumber: 1, pageSize: 10));
    }

    [Fact]
    public async Task GetContributorForEditAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "userName": "carrie.batten",
              "fullName": "Carrie Batten",
              "organisation": "Pirbright Institute",
              "roleId": "11111111-1111-1111-1111-111111111111",
              "isSsoUser": true,
              "sectionPermissionIds": ["22222222-2222-2222-2222-222222222222"],
              "lastUpdated": "AQIDBAUGBwg="
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.GetContributorForEditAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal("carrie.batten", result!.UserName);
        Assert.True(result.IsSsoUser);
        Assert.Single(result.SectionPermissionIds);
    }

    [Fact]
    public async Task GetContributorForEditAsync_ReturnsNull_WhenTheApiReturnsNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var result = await service.GetContributorForEditAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetContributorForEditAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetContributorForEditAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProfileUserRolesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              { "id": "11111111-1111-1111-1111-111111111111", "name": "Technical author", "isContributor": true }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.GetProfileUserRolesAsync();

        var role = Assert.Single(result);
        Assert.Equal("Technical author", role.Name);
        Assert.True(role.IsContributor);
    }

    [Fact]
    public async Task GetProfileUserRolesAsync_ReturnsEmpty_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.GetProfileUserRolesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsSuccess_WhenTheApiReturnsNoContent()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        var result = await service.UpdateContributorAsync(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateRequest());

        Assert.Equal(ContributorUpdateOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsConflict_WhenTheApiReturnsConflict()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await service.UpdateContributorAsync(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateRequest());

        Assert.Equal(ContributorUpdateOutcome.Conflict, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WithTheProblemDetail()
    {
        const string json = """{ "detail": "Please select a valid role." }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, json));

        var result = await service.UpdateContributorAsync(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateRequest());

        Assert.Equal(ContributorUpdateOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("Please select a valid role.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WithAFallbackMessage_WhenTheBodyIsNotProblemJson()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, "not-json"));

        var result = await service.UpdateContributorAsync(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateRequest());

        Assert.Equal(ContributorUpdateOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("Please correct the highlighted fields.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.UpdateContributorAsync(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateRequest());

        Assert.Equal(ContributorUpdateOutcome.Error, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task VerifyContributorUsernameAsync_DeserialisesTheResponseBody()
    {
        const string json = """{ "outcome": 0, "userId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f" }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.VerifyContributorUsernameAsync("internal\\carrie.batten");

        Assert.Equal(UserVerificationOutcome.ExistingUser, result.Outcome);
        Assert.Equal(Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f"), result.UserId);
    }

    [Fact]
    public async Task VerifyContributorUsernameAsync_ReturnsInvalidFormat_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.VerifyContributorUsernameAsync("not-valid");

        Assert.Equal(UserVerificationOutcome.InvalidFormat, result.Outcome);
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsSuccess_WhenTheApiReturnsNoContent()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        var result = await service.AddContributorAsync(Guid.NewGuid(), CreateAddRequest());

        Assert.Equal(ContributorAddOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsConflict_WhenTheApiReturnsConflict()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await service.AddContributorAsync(Guid.NewGuid(), CreateAddRequest());

        Assert.Equal(ContributorAddOutcome.Conflict, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsValidationFailed_WithTheProblemDetail()
    {
        const string json = """{ "detail": "Please select a valid role." }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, json));

        var result = await service.AddContributorAsync(Guid.NewGuid(), CreateAddRequest());

        Assert.Equal(ContributorAddOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("Please select a valid role.", result.ErrorMessage);
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.AddContributorAsync(Guid.NewGuid(), CreateAddRequest());

        Assert.Equal(ContributorAddOutcome.Error, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task DeleteContributorAsync_ReturnsSuccess_WhenTheApiReturnsNoContent()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        var result = await service.DeleteContributorAsync(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(ContributorDeleteOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task DeleteContributorAsync_ReturnsConflict_WhenTheApiReturnsConflict()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await service.DeleteContributorAsync(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(ContributorDeleteOutcome.Conflict, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task DeleteContributorAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.DeleteContributorAsync(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(ContributorDeleteOutcome.Error, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    private static UpdateContributorRequest CreateUpdateRequest() => new()
    {
        RoleId = Guid.NewGuid(),
        FullName = "Carrie Batten",
        Organisation = "Pirbright Institute",
        SectionPermissionIds = [],
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    private static AddContributorRequest CreateAddRequest() => new()
    {
        ContributorId = Guid.NewGuid(),
        UserName = "internal\\new.user",
        IsSsoUser = false,
        RoleId = Guid.NewGuid(),
        FullName = "New User",
        Organisation = "Pirbright Institute",
        SectionPermissionIds = []
    };

    private static ProfileContributorsApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new ProfileContributorsApiService(httpClient);
    }
}
