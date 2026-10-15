namespace CDC.Api.Features.BespokeReportTemplates;

/// <summary>
/// Source-generated structured log messages for the bespoke report templates feature. Template
/// titles are not logged: only identifiers and counts are.
/// </summary>
internal static partial class BespokeReportTemplateLog
{
    [LoggerMessage(EventId = 6100, Level = LogLevel.Information, Message = "Retrieved {TemplateCount} bespoke report templates")]
    public static partial void RetrievedTemplates(this ILogger logger, int templateCount);

    [LoggerMessage(EventId = 6101, Level = LogLevel.Information, Message = "Saved bespoke report template {TemplateId} with {CheckedCount} ticked items")]
    public static partial void SavedTemplate(this ILogger logger, Guid templateId, int checkedCount);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Information, Message = "Updated bespoke report template {TemplateId} to {CheckedCount} ticked items")]
    public static partial void UpdatedTemplate(this ILogger logger, Guid templateId, int checkedCount);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Information, Message = "Renamed bespoke report template {TemplateId}")]
    public static partial void RenamedTemplate(this ILogger logger, Guid templateId);

    [LoggerMessage(EventId = 6104, Level = LogLevel.Information, Message = "Deleted bespoke report template {TemplateId}")]
    public static partial void DeletedTemplate(this ILogger logger, Guid templateId);

    [LoggerMessage(EventId = 6105, Level = LogLevel.Warning, Message = "A bespoke report template request was refused: the user may not {Operation} templates")]
    public static partial void TemplateRequestRefused(this ILogger logger, string operation);

    [LoggerMessage(EventId = 6106, Level = LogLevel.Warning, Message = "A bespoke report template was not saved: its title is already in use")]
    public static partial void TemplateTitleInUse(this ILogger logger);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Error, Message = "Bespoke report template command {Command} failed")]
    public static partial void CommandFailed(this ILogger logger, Exception exception, string command);
}
