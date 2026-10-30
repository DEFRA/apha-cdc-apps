using System.Net;
using System.Net.Http.Json;

namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// Default <see cref="ICustomWordsApiService"/>. The base address is configured on the underlying <see cref="HttpClient"/> in
/// Program.cs (from <c>Api:BaseUrl</c>), so no URL is hardcoded here. A failure to read the words is left to propagate, for the
/// caller to decide what to do without them.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class CustomWordsApiService(HttpClient httpClient) : ICustomWordsApiService
{
    private const string CustomWordsPath = "/api/spell-check/custom-words";

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken = default)
    {
        var words = await httpClient.GetFromJsonAsync<IReadOnlyList<string>>(CustomWordsPath, cancellationToken);

        return words ?? [];
    }

    /// <inheritdoc />
    public async Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(CustomWordsPath, new { Word = word }, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return AddCustomWordOutcome.Added;
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => AddCustomWordOutcome.Forbidden,
            HttpStatusCode.BadRequest => AddCustomWordOutcome.Invalid,
            _ => AddCustomWordOutcome.Failed
        };
    }
}
