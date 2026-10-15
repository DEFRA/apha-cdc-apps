namespace CDC.Web.Infrastructure.Reports;

/// <summary>Builds the PDF for one kind of profile report, selected by its internal report name.</summary>
public interface IProfileReportPdfBuilder
{
    /// <summary>Gets the internal report name this builder produces, for example <c>SummaryProfileGUID</c>.</summary>
    string ReportName { get; }

    /// <summary>Builds the report's PDF for a profile version.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The PDF.</returns>
    Task<byte[]> BuildAsync(Guid profileVersionId, CancellationToken cancellationToken);
}

/// <summary>Generates profile report PDFs by report name, replacing the legacy <c>CreateProfilePdf</c> switch.</summary>
public interface IProfileReportGenerator
{
    /// <summary>Gets a value indicating whether a report can be generated for the internal report name.</summary>
    /// <param name="reportName">The internal report name.</param>
    /// <returns><see langword="true"/> when a builder is registered for it.</returns>
    bool CanGenerate(string reportName);

    /// <summary>Generates a report's PDF.</summary>
    /// <param name="reportName">The internal report name.</param>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the generation.</param>
    /// <returns>The PDF.</returns>
    /// <exception cref="NotSupportedException">No builder is registered for <paramref name="reportName"/>.</exception>
    Task<byte[]> GenerateAsync(string reportName, Guid profileVersionId, CancellationToken cancellationToken);
}

/// <summary>Default <see cref="IProfileReportGenerator"/>: dispatches to the registered <see cref="IProfileReportPdfBuilder"/> for the name.</summary>
/// <param name="builders">Every registered report builder.</param>
public sealed class ProfileReportGenerator(IEnumerable<IProfileReportPdfBuilder> builders) : IProfileReportGenerator
{
    private readonly Dictionary<string, IProfileReportPdfBuilder> buildersByName =
        builders.ToDictionary(builder => builder.ReportName, StringComparer.Ordinal);

    /// <inheritdoc />
    public bool CanGenerate(string reportName) => buildersByName.ContainsKey(reportName);

    /// <inheritdoc />
    public Task<byte[]> GenerateAsync(string reportName, Guid profileVersionId, CancellationToken cancellationToken)
    {
        if (!buildersByName.TryGetValue(reportName, out var builder))
        {
            throw new NotSupportedException($"The report type '{reportName}' is not supported");
        }

        return builder.BuildAsync(profileVersionId, cancellationToken);
    }
}
