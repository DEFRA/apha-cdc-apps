using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.TestSupport;

// An in-memory ISession for page-model tests, which run without the session middleware. Share one between two page
// models to stand for two requests of the same user.
internal sealed class FakeSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = [];

    public bool IsAvailable => true;

    public string Id => "test-session";

    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Remove(string key) => _store.Remove(key);

    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}
