using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.UserAdmin;

/// <summary>In-memory <see cref="IUserAdminApiService"/> for the user administration page tests.</summary>
internal sealed class FakeUserAdminApiService(
    IReadOnlyList<MaintainedUserDto>? users = null,
    MaintainedUserDto? user = null,
    UpdateReviewEmailSubscriptionResult? updateResult = null,
    Exception? throwOnRead = null) : IUserAdminApiService
{
    public UpdateReviewEmailSubscriptionRequest? LastUpdateRequest { get; private set; }

    public Task<IReadOnlyList<MaintainedUserDto>> GetGlobalUsersAsync(CancellationToken cancellationToken = default) =>
        throwOnRead is not null ? Task.FromException<IReadOnlyList<MaintainedUserDto>>(throwOnRead) : Task.FromResult(users ?? []);

    public Task<IReadOnlyList<MaintainedUserDto>> GetExternalUsersAsync(CancellationToken cancellationToken = default) =>
        throwOnRead is not null ? Task.FromException<IReadOnlyList<MaintainedUserDto>>(throwOnRead) : Task.FromResult(users ?? []);

    public Task<MaintainedUserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        throwOnRead is not null ? Task.FromException<MaintainedUserDto?>(throwOnRead) : Task.FromResult(user);

    public Task<UpdateReviewEmailSubscriptionResult> UpdateReviewEmailSubscriptionAsync(
        UpdateReviewEmailSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = request;

        return Task.FromResult(updateResult ?? new UpdateReviewEmailSubscriptionResult(ReviewEmailSubscriptionOutcome.Success, null));
    }
}
