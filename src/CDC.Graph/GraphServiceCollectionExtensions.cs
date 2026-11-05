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
    private static readonly Uri GraphBaseAddress = new("https://graph.microsoft.com/v1.0/");

    /// <summary>
    /// Adds <see cref="IGraphClient"/>, bound to the <c>Graph</c> configuration section and
    /// falling back to the shared Entra app registration values when the explicit Graph values are absent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddGraphClient(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(GraphOptions.SectionName);

        services.AddOptions<GraphOptions>()
            .Bind(section)
            .Configure(options =>
            {
                var sharedTenantId = configuration["Entra:TenantId"]
                    ?? configuration["SsoSettings:TenantId"]
                    ?? string.Empty;
                var sharedClientId = configuration["Entra:ClientId"]
                    ?? configuration["SsoSettings:ClientId"]
                    ?? string.Empty;
                var sharedClientSecret = configuration["Entra:ClientSecret"]
                    ?? configuration["SsoSettings:ClientSecret"]
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(options.TenantId) && !string.IsNullOrWhiteSpace(sharedTenantId))
                {
                    options.TenantId = sharedTenantId;
                }

                if (string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(sharedClientId))
                {
                    options.ClientId = sharedClientId;
                }

                if (string.IsNullOrWhiteSpace(options.ClientSecret) && !string.IsNullOrWhiteSpace(sharedClientSecret))
                {
                    options.ClientSecret = sharedClientSecret;
                }
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GraphOptions>, GraphOptionsValidator>();

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
}
