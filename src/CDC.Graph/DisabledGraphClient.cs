namespace CDC.Graph;

/// <summary>Used when <c>Graph:Enabled</c> is false: finds nobody, so callers fall back to their non-Graph behaviour.</summary>
internal sealed class DisabledGraphClient : IGraphClient
{
    /// <inheritdoc />
    public Task<GraphUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken) =>
        Task.FromResult<GraphUser?>(null);
}
