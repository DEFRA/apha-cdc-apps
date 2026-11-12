namespace CDC.Graph;

/// <summary>Compatibility no-op client retained for callers that need a safe fallback when Graph lookups are intentionally disabled elsewhere.</summary>
internal sealed class DisabledGraphClient : IGraphClient
{
    /// <inheritdoc />
    public Task<GraphUser?> FindUserBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken) =>
        Task.FromResult<GraphUser?>(null);

    /// <inheritdoc />
    public Task<string?> FindEmployeeIdAsync(Guid objectId, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
