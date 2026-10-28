using System.Globalization;
using CDC.Common.Contracts;
using CDC.Web.Models;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Builds the "All Revision dates" tab's rows from CDC.Api's data, and reads and validates the
/// submitted form back into the save request. Carries the legacy
/// <c>EditProfileAllRevisionDates.aspx</c> rules: each "Next ... review" date must be a valid date
/// (the legacy <c>DateValidator</c>), within the range the database column supports; and each
/// frequency/status must be one of the offered options.
/// </summary>
public static class ProfileRevisionDatesForm
{
    /// <summary>The date format shown and accepted, matching the legacy calendar extender.</summary>
    public const string DateFormat = "d MMMM yyyy";

    /// <summary>The earliest date accepted (the database column's lower bound).</summary>
    public static readonly DateTime MinimumDate = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The latest date accepted (the database column's upper bound).</summary>
    public static readonly DateTime MaximumDate = new(2079, 6, 6, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly string[] AcceptedDateFormats =
    [
        "d MMMM yyyy", "d MMM yyyy", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "yyyy-MM-dd"
    ];

    /// <summary>Reads a date typed into a date field, in any of the formats the field accepts.</summary>
    /// <param name="value">The text typed.</param>
    /// <param name="date">The date read, when <see langword="true"/> is returned.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a date in one of the accepted formats.</returns>
    public static bool TryParseDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value?.Trim(), AcceptedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>Builds the drop-down options from a lookup list, led by the legacy "Please select..." blank option.</summary>
    /// <param name="items">The lookup items from CDC.Api.</param>
    /// <returns>The options, with a leading blank option.</returns>
    public static IReadOnlyList<RevisionDatesOption> BuildOptions(IReadOnlyList<ReviewLookupItemDto> items) =>
        [new RevisionDatesOption(string.Empty, "Please select..."), .. items.Select(item => new RevisionDatesOption(item.Id.ToString(), item.Name))];

    /// <summary>Builds one row per section from the data read from CDC.Api.</summary>
    /// <param name="source">The revision dates read from CDC.Api.</param>
    /// <returns>The rows, in the order CDC.Api returned the sections.</returns>
    public static IReadOnlyList<RevisionDatesRowView> BuildRows(ProfileVersionAllRevisionDatesDto source) =>
        [.. source.Sections.Select(section => StoredRow(section, Convert.ToBase64String(section.LastUpdated)))];

    /// <summary>Gets the posted field name for one control of one section's row.</summary>
    /// <param name="sectionId">The section.</param>
    /// <param name="field">One of the <c>Field*</c> constants.</param>
    /// <returns>The form field name.</returns>
    public static string FieldName(Guid sectionId, string field) => $"rev_{sectionId:N}_{field}";

    /// <summary>Gets the posted field name for one "Set For All Sections" control.</summary>
    /// <param name="field">One of the <c>Field*</c> constants (a status or next review field).</param>
    /// <returns>The form field name.</returns>
    public static string SetForAllFieldName(string field) => $"revall_{field}";

    /// <summary>Field key of the author review frequency control.</summary>
    public const string FieldAuthorFrequency = "AuthorFreq";

    /// <summary>Field key of the VA review frequency control.</summary>
    public const string FieldVaFrequency = "VaFreq";

    /// <summary>Field key of the policy review frequency control.</summary>
    public const string FieldPolicyFrequency = "PolicyFreq";

    /// <summary>Field key of the next author review date control.</summary>
    public const string FieldNextAuthor = "NextAuthor";

    /// <summary>Field key of the next VA review date control.</summary>
    public const string FieldNextVa = "NextVa";

    /// <summary>Field key of the next policy review date control.</summary>
    public const string FieldNextPolicy = "NextPolicy";

    /// <summary>Field key of the author review status control.</summary>
    public const string FieldAuthorStatus = "AuthorStatus";

    /// <summary>Field key of the VA review status control.</summary>
    public const string FieldVaStatus = "VaStatus";

    /// <summary>Field key of the policy review status control.</summary>
    public const string FieldPolicyStatus = "PolicyStatus";

    /// <summary>Field key of the hidden row version.</summary>
    public const string FieldLastUpdated = "LastUpdated";

    /// <summary>Reads the "Set For All Sections" values back from the form, so a re-rendered page keeps them.</summary>
    /// <param name="form">The submitted form.</param>
    /// <returns>The posted values (empty text where absent).</returns>
    public static RevisionDatesSetForAllView ReadSetForAll(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        return new RevisionDatesSetForAllView
        {
            AuthorStatus = Read(form, SetForAllFieldName(FieldAuthorStatus)),
            NextAuthorReview = Read(form, SetForAllFieldName(FieldNextAuthor)),
            VaStatus = Read(form, SetForAllFieldName(FieldVaStatus)),
            NextVaReview = Read(form, SetForAllFieldName(FieldNextVa)),
            PolicyStatus = Read(form, SetForAllFieldName(FieldPolicyStatus)),
            NextPolicyReview = Read(form, SetForAllFieldName(FieldNextPolicy))
        };
    }

    /// <summary>
    /// Reads every section's submitted values and validates them against the options CDC.Api
    /// offered. A review the user may not change keeps the value CDC.Api holds, whatever the form posted, so a hand-made
    /// post cannot change it and a tab that shows it read-only does not have to post it.
    /// </summary>
    /// <param name="form">The submitted form.</param>
    /// <param name="source">The revision dates freshly read from CDC.Api (section list and option lists).</param>
    /// <param name="editable">The reviews the user may change; every review when omitted.</param>
    /// <returns>The submitted rows (with their errors), every error message, and the save request.</returns>
    public static RevisionDatesParseResult Parse(IFormCollection form, ProfileVersionAllRevisionDatesDto source, EditableReviews? editable = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(source);

        var changeable = editable ?? EditableReviews.All;
        var validFrequencies = source.ReviewFrequencies.Select(item => item.Id).ToHashSet();
        var validStatuses = source.ReviewStatuses.Select(item => item.Id).ToHashSet();

        var rows = new List<RevisionDatesRowView>();
        var inputs = new List<ProfileSectionRevisionDatesInputDto>();
        var allErrors = new List<string>();

        foreach (var section in source.Sections)
        {
            var (row, input) = ParseSection(form, section, changeable, validFrequencies, validStatuses);
            rows.Add(row);
            allErrors.AddRange(row.Errors);

            if (input is not null)
            {
                inputs.Add(input);
            }
        }

        return new RevisionDatesParseResult(rows, allErrors, inputs);
    }

    private static (RevisionDatesRowView Row, ProfileSectionRevisionDatesInputDto? Input) ParseSection(
        IFormCollection form,
        ProfileSectionRevisionDatesDto section,
        EditableReviews changeable,
        HashSet<Guid> validFrequencies,
        HashSet<Guid> validStatuses)
    {
        var row = ReadRow(form, section, changeable);
        var errors = new List<string>();

        var lastUpdated = ParseRowVersion(row.LastUpdated);
        if (lastUpdated is null)
        {
            errors.Add("the section's revision data could not be read. Reload the page and try again.");
        }

        // Frequencies, then statuses, then dates: the order a section's messages are reported in.
        var input = ParseFrequencies(StoredInput(section), row, changeable, validFrequencies, errors);
        input = ParseStatuses(input, row, changeable, validStatuses, errors);
        input = ParseDates(input, row, changeable, errors);

        var prefixedErrors = errors.Select(error => $"{row.SectionDisplay}: {error}").ToList();
        var rowWithErrors = row with { Errors = prefixedErrors };

        if (prefixedErrors.Count > 0 || lastUpdated is null)
        {
            return (rowWithErrors, null);
        }

        return (rowWithErrors, input with { LastUpdated = lastUpdated });
    }

    private static RevisionDatesRowView ReadRow(IFormCollection form, ProfileSectionRevisionDatesDto section, EditableReviews changeable)
    {
        var row = StoredRow(section, ReadField(form, section, FieldLastUpdated));

        if (changeable.Author)
        {
            row = row with
            {
                AuthorFrequency = ReadField(form, section, FieldAuthorFrequency),
                NextAuthorReview = ReadField(form, section, FieldNextAuthor),
                AuthorStatus = ReadField(form, section, FieldAuthorStatus)
            };
        }

        if (changeable.Va)
        {
            row = row with
            {
                VaFrequency = ReadField(form, section, FieldVaFrequency),
                NextVaReview = ReadField(form, section, FieldNextVa),
                VaStatus = ReadField(form, section, FieldVaStatus)
            };
        }

        if (changeable.Policy)
        {
            row = row with
            {
                PolicyFrequency = ReadField(form, section, FieldPolicyFrequency),
                NextPolicyReview = ReadField(form, section, FieldNextPolicy),
                PolicyStatus = ReadField(form, section, FieldPolicyStatus)
            };
        }

        return row;
    }

    private static RevisionDatesRowView StoredRow(ProfileSectionRevisionDatesDto section, string lastUpdated) => new()
    {
        ProfileSectionId = section.ProfileSectionId,
        SectionDisplay = SectionDisplay(section),
        AuthorFrequency = IdText(section.AuthorReviewFrequencyId),
        VaFrequency = IdText(section.VaReviewFrequencyId),
        PolicyFrequency = IdText(section.PolicyReviewFrequencyId),
        NextAuthorReview = DateText(section.NextAuthorReview),
        NextVaReview = DateText(section.NextVaReview),
        NextPolicyReview = DateText(section.NextPolicyReview),
        AuthorStatus = IdText(section.AuthorReviewStatusId),
        VaStatus = IdText(section.VaReviewStatusId),
        PolicyStatus = IdText(section.PolicyReviewStatusId),
        LastUpdated = lastUpdated
    };

    // The save request that leaves the section as CDC.Api holds it; what the user may change replaces the values it covers.
    private static ProfileSectionRevisionDatesInputDto StoredInput(ProfileSectionRevisionDatesDto section) => new()
    {
        ProfileSectionId = section.ProfileSectionId,
        AuthorReviewFrequencyId = section.AuthorReviewFrequencyId,
        VaReviewFrequencyId = section.VaReviewFrequencyId,
        PolicyReviewFrequencyId = section.PolicyReviewFrequencyId,
        NextAuthorReview = section.NextAuthorReview,
        NextVaReview = section.NextVaReview,
        NextPolicyReview = section.NextPolicyReview,
        AuthorReviewStatusId = section.AuthorReviewStatusId,
        VaReviewStatusId = section.VaReviewStatusId,
        PolicyReviewStatusId = section.PolicyReviewStatusId,
        LastUpdated = section.LastUpdated
    };

    private static ProfileSectionRevisionDatesInputDto ParseFrequencies(
        ProfileSectionRevisionDatesInputDto input,
        RevisionDatesRowView row,
        EditableReviews changeable,
        HashSet<Guid> validIds,
        List<string> errors)
    {
        return input with
        {
            AuthorReviewFrequencyId = changeable.Author ? ParseOption(row.AuthorFrequency, validIds, "Author review frequency", errors) : input.AuthorReviewFrequencyId,
            VaReviewFrequencyId = changeable.Va ? ParseOption(row.VaFrequency, validIds, "VA review frequency", errors) : input.VaReviewFrequencyId,
            PolicyReviewFrequencyId = changeable.Policy ? ParseOption(row.PolicyFrequency, validIds, "Policy review frequency", errors) : input.PolicyReviewFrequencyId
        };
    }

    private static ProfileSectionRevisionDatesInputDto ParseStatuses(
        ProfileSectionRevisionDatesInputDto input,
        RevisionDatesRowView row,
        EditableReviews changeable,
        HashSet<Guid> validIds,
        List<string> errors)
    {
        return input with
        {
            AuthorReviewStatusId = changeable.Author ? ParseOption(row.AuthorStatus, validIds, "Author review status", errors) : input.AuthorReviewStatusId,
            VaReviewStatusId = changeable.Va ? ParseOption(row.VaStatus, validIds, "VA review status", errors) : input.VaReviewStatusId,
            PolicyReviewStatusId = changeable.Policy ? ParseOption(row.PolicyStatus, validIds, "Policy review status", errors) : input.PolicyReviewStatusId
        };
    }

    private static ProfileSectionRevisionDatesInputDto ParseDates(
        ProfileSectionRevisionDatesInputDto input,
        RevisionDatesRowView row,
        EditableReviews changeable,
        List<string> errors)
    {
        return input with
        {
            NextAuthorReview = changeable.Author ? ParseDate(row.NextAuthorReview, "Next author review date", errors) : input.NextAuthorReview,
            NextVaReview = changeable.Va ? ParseDate(row.NextVaReview, "Next VA review date", errors) : input.NextVaReview,
            NextPolicyReview = changeable.Policy ? ParseDate(row.NextPolicyReview, "Next policy review date", errors) : input.NextPolicyReview
        };
    }

    private static string SectionDisplay(ProfileSectionRevisionDatesDto section) => $"{section.SectionNumber}. {section.SectionName}";

    private static string IdText(Guid? id) => id?.ToString() ?? string.Empty;

    private static string DateText(DateTime? date) => date?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Read(IFormCollection form, string key) => form[key].ToString().Trim();

    private static string ReadField(IFormCollection form, ProfileSectionRevisionDatesDto section, string field) =>
        Read(form, FieldName(section.ProfileSectionId, field));

    private static byte[]? ParseRowVersion(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(value);

            return bytes.Length == 8 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>A blank selection means "not set" (null); otherwise it must be one of the offered options.</summary>
    private static Guid? ParseOption(string value, HashSet<Guid> validIds, string label, List<string> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var id) && validIds.Contains(id))
        {
            return id;
        }

        errors.Add($"{label} is not a valid option.");

        return null;
    }

    /// <summary>A blank date means "not set" (null), as in the legacy page.</summary>
    private static DateTime? ParseDate(string value, string label, List<string> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!TryParseDate(value, out var date))
        {
            errors.Add($"{label} must be a valid date");

            return null;
        }

        if (date.Date < MinimumDate || date.Date > MaximumDate)
        {
            errors.Add($"{label} must be between {MinimumDate.ToString(DateFormat, CultureInfo.InvariantCulture)} and {MaximumDate.ToString(DateFormat, CultureInfo.InvariantCulture)}");

            return null;
        }

        return date.Date;
    }
}

/// <summary>The result of reading and validating the submitted "All Revision dates" form.</summary>
/// <param name="Rows">Every section's submitted values (with their errors), for re-rendering.</param>
/// <param name="Errors">Every validation message, prefixed with its section.</param>
/// <param name="Inputs">The save request for each valid section; only meaningful when there are no errors.</param>
public sealed record RevisionDatesParseResult(
    IReadOnlyList<RevisionDatesRowView> Rows,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProfileSectionRevisionDatesInputDto> Inputs)
{
    /// <summary>Gets a value indicating whether every section's submission was valid.</summary>
    public bool IsValid => Errors.Count == 0;
}
