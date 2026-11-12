using Microsoft.Extensions.Options;

namespace CDC.Web.Features.LoadTesting;

/// <summary>Fails fast at startup when LoadTesting is switched on where it must not be, or without what it needs.</summary>
/// <param name="configuration">The application configuration, which names the deployment.</param>
/// <param name="environment">The hosting environment, to recognise a local run.</param>
/// <param name="timeProvider">The source of the current time.</param>
public sealed class LoadTestingOptionsValidator(IConfiguration configuration, IHostEnvironment environment, TimeProvider timeProvider)
    : IValidateOptions<LoadTestingOptions>
{
    /// <summary>The configuration key naming the deployment (DEV, TEST, PRE-PROD or PROD), set on every ECS task definition.</summary>
    public const string DeploymentKey = "Deployment:Environment";

    // Every AWS task runs with ASPNETCORE_ENVIRONMENT=Production, so only this label tells a test environment from production.
    private static readonly string[] AllowedDeployments = ["DEV", "TEST", "PRE-PROD"];

    private static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(7);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, LoadTestingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        var deployment = configuration[DeploymentKey]?.Trim();
        var isDeployed = !string.IsNullOrEmpty(deployment);

        ValidateDeployment(deployment, isDeployed, failures);
        ValidateWindow(options.ExpiresAtUtc, isDeployed, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateDeployment(string? deployment, bool isDeployed, List<string> failures)
    {
        var allowed = string.Join(", ", AllowedDeployments);

        if (isDeployed && !AllowedDeployments.Contains(deployment, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"{LoadTestingOptions.SectionName} cannot be enabled in the '{deployment}' deployment; it is allowed only in {allowed}.");
        }
        else if (!isDeployed && !environment.IsDevelopment())
        {
            failures.Add($"{LoadTestingOptions.SectionName} can be enabled outside a local Development run only when {DeploymentKey} is set to one of {allowed}.");
        }
    }

    private void ValidateWindow(DateTimeOffset? expiresAtUtc, bool isDeployed, List<string> failures)
    {
        var key = $"{LoadTestingOptions.SectionName}:{nameof(LoadTestingOptions.ExpiresAtUtc)}";

        if (expiresAtUtc is null)
        {
            if (isDeployed)
            {
                failures.Add($"{key} is required in a deployed environment, so the bypass switches itself off.");
            }
        }
        else if (expiresAtUtc > timeProvider.GetUtcNow() + MaximumWindow)
        {
            failures.Add($"{key} must be no more than {MaximumWindow.TotalDays} days from now.");
        }
    }
}
