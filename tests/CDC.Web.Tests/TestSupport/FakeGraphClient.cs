using CDC.Graph;

namespace CDC.Web.Tests.TestSupport;

internal sealed class FakeGraphClient : IGraphClient
{
    public GraphUser? User { get; init; } = new("id-1", "newuser", null, "  New", "User  ");

    public bool Throw { get; init; }

    public string? EmployeeId { get; init; }

    public bool ThrowOnEmployeeId { get; init; }

    public string? RequestedSamAccountName { get; private set; }

    public Guid? RequestedObjectId { get; private set; }

    public Task<GraphUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken)
    {
        RequestedSamAccountName = samAccountName;

        return Throw ? Task.FromException<GraphUser?>(new GraphException("down")) : Task.FromResult(User);
    }

    public Task<string?> FindEmployeeIdAsync(Guid objectId, CancellationToken cancellationToken)
    {
        RequestedObjectId = objectId;

        return ThrowOnEmployeeId ? Task.FromException<string?>(new GraphException("down")) : Task.FromResult(EmployeeId);
    }
}
