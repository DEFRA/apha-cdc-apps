using Microsoft.Extensions.Options;

namespace CDC.Graph.Options;

/// <summary>Fails fast at startup when Graph is enabled but its credentials are incomplete.</summary>
public sealed class GraphOptionsValidator : IValidateOptions<GraphOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GraphOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        RequireNotEmpty(options.TenantId, nameof(GraphOptions.TenantId), failures);
        RequireNotEmpty(options.ClientId, nameof(GraphOptions.ClientId), failures);
        RequireNotEmpty(options.ClientSecret, nameof(GraphOptions.ClientSecret), failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void RequireNotEmpty(string value, string propertyName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{nameof(GraphOptions)}.{propertyName} is required when Graph is enabled.");
        }
    }
}
