using Azure.Core;
using Azure.Identity;
using CDC.Graph.Authentication;
using CDC.Graph.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CDC.Graph;

/// <summary>Registers the shared Microsoft Graph client.</summary>
public static class GraphServiceCollectionExtensions
{
    private const string EntraSectionName = "Entra";

    private static readonly Uri GraphBaseAddress = new("https://graph.microsoft.com/v1.0/");

    /// <summary>
    /// Adds <see cref="IGraphClient"/>, bound to the <c>Graph</c> configuration section. When
    /// <c>Graph:Enabled</c> is false a no-op client is registered and no credentials are required.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddGraphClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(GraphOptions.SectionName);
        var entraSection = configuration.GetSection(EntraSectionName);

        // Graph reuses the Entra app registration unless Graph-specific credentials are supplied.
        services.AddOptions<GraphOptions>()
            .Bind(section)
            .PostConfigure(options =>
            {
                options.TenantId = FirstNonEmpty(options.TenantId, entraSection[nameof(GraphOptions.TenantId)]);
                options.ClientId = FirstNonEmpty(options.ClientId, entraSection[nameof(GraphOptions.ClientId)]);
                options.ClientSecret = FirstNonEmpty(options.ClientSecret, entraSection[nameof(GraphOptions.ClientSecret)]);
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GraphOptions>, GraphOptionsValidator>();

        if (!section.GetValue<bool>(nameof(GraphOptions.Enabled)))
        {
            services.AddSingleton<IGraphClient, DisabledGraphClient>();

            return services;
        }

        // Singleton so the one credential's in-memory token cache is shared by every request.
        services.AddSingleton<TokenCredential>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<GraphOptions>>().Value;

            return new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret);
        });
        services.AddSingleton<IGraphTokenProvider, GraphTokenProvider>();
        services.AddTransient<GraphAuthorizationHandler>();

        // Resilience is added first so it is the outer handler: each retry then passes back through
        // the authorization handler and picks up a current token.
        var clientBuilder = services.AddHttpClient<IGraphClient, GraphClient>(client => client.BaseAddress = GraphBaseAddress);
        clientBuilder.AddStandardResilienceHandler();
        clientBuilder.AddHttpMessageHandler<GraphAuthorizationHandler>();

        return services;
    }

    private static string FirstNonEmpty(string value, string? fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback ?? string.Empty : value;
}
