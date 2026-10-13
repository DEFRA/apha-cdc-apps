namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Thrown when an upload breaks a business rule enforced by the database, for example reusing the
/// title of an existing user manual for a general report.
/// </summary>
public sealed class StaticReportConflictException(string message) : Exception(message);
