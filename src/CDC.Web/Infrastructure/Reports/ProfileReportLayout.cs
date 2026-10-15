using System.Globalization;
using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>Shared layout rules for the profile report PDFs: the document frame, page header and footer, and question lookups.</summary>
public static class ProfileReportLayout
{
    /// <summary>The legacy "Please Note" heading printed on the last page of every profile report.</summary>
    public const string WarningTitle = "Please Note";

    /// <summary>The legacy <c>BuildWarningTextSection</c> text.</summary>
    public const string WarningText =
        "The content in a number of questions constitutes static information, or is no longer deemed critical, and, to minimise the resource requirements of D2R2, is not scheduled to be updated. Please see the D2R2 quality statement for a list of these questions.";

    /// <summary>The legacy front-page line that points internal users at the other reports.</summary>
    public const string InternalReportsLinkText = "For internal Defra users other reports are available on: ";

    /// <summary>Finds a question by its section and question numbers, as the legacy <c>ProfileVersionQuestionData(section, question)</c> did.</summary>
    /// <param name="content">The profile version's report content.</param>
    /// <param name="sectionNumber">The section number.</param>
    /// <param name="questionNumber">The question number within the section.</param>
    /// <returns>The question, or <see langword="null"/> when there is none.</returns>
    public static ProfileReportQuestionDto? FindQuestion(ProfileReportContentDto content, int sectionNumber, int questionNumber) =>
        content.Sections
            .FirstOrDefault(section => section.SectionNumber == sectionNumber)?
            .Questions.FirstOrDefault(question => question.QuestionNumber == questionNumber);

    /// <summary>
    /// Reads a question's answer the way the legacy <c>FieldValues(0).ToString</c> did: the text of the
    /// first field's selected option, or its text when it is a text field.
    /// </summary>
    /// <param name="question">The question to read.</param>
    /// <returns>The answer text, or empty when unanswered.</returns>
    public static string FirstAnswerText(ProfileReportQuestionDto? question)
    {
        if (question is not { Groups: [{ Fields: [var field, ..] }, ..] })
        {
            return string.Empty;
        }

        return field.Kind == ProfileReportFieldKind.Options
            ? field.Options.FirstOrDefault(option => option.Selected)?.Text ?? string.Empty
            : field.Text;
    }

    /// <summary>Creates a report document from a profile version's content and its laid-out sections.</summary>
    /// <param name="reportTitle">The report's name.</param>
    /// <param name="content">The profile version's report content.</param>
    /// <param name="sections">The body sections.</param>
    /// <param name="showFrontPage">Whether the title page and contents list are printed.</param>
    /// <param name="references">The landscape reference tables, or <see langword="null"/>.</param>
    /// <returns>The document, ready for the Razor view.</returns>
    public static ProfileReportDocumentViewModel CreateDocument(
        string reportTitle,
        ProfileReportContentDto content,
        IReadOnlyList<ReportSectionView> sections,
        bool showFrontPage = true,
        IReadOnlyList<ReportReferenceSectionView>? references = null) =>
        new(
            reportTitle,
            content.FullTitle,
            content.VersionLabel,
            content.IsPublished ? content.EffectiveDateFrom.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) : null,
            content.ShowDraftWatermark,
            showFrontPage,
            sections,
            references);

    /// <summary>Builds a question table's view, plain-texting the name as the legacy captions did.</summary>
    /// <param name="number">The number shown before the name, for example "2.1".</param>
    /// <param name="name">The name shown after the number.</param>
    /// <param name="question">The question and its answers.</param>
    /// <param name="additionalInfo">An italic line under the heading, or <see langword="null"/>.</param>
    /// <param name="level">The heading level.</param>
    /// <returns>The question table.</returns>
    public static ReportQuestionView Question(string number, string name, ProfileReportQuestionDto question, string? additionalInfo = null, int level = 1) =>
        new($"{number} {ProfileTitleHtmlFormatter.ToPlainText(name)}", additionalInfo, question, level);

    /// <summary>Builds the page header and footer: the report name and profile title on top, "Page x of y" below.</summary>
    /// <param name="document">The document the options are for.</param>
    /// <returns>The render options.</returns>
    public static PdfRenderOptions BuildPdfOptions(ProfileReportDocumentViewModel document) => new()
    {
        HeaderHtml = BuildHeaderHtml(document.ReportTitle, document.ProfileTitle),
        FooterHtml =
            "<div style=\"width:100%;font-family:Arial,Helvetica,sans-serif;font-size:9px;text-align:center;\">" +
            "Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>",
        MarginTopMm = 30,
        MarginBottomMm = 20,
        GenerateOutline = true,
        PreferCssPageSize = true
    };

    /// <summary>Builds the page header: the report name and the profile title, over a rule.</summary>
    /// <param name="reportTitle">The report's name.</param>
    /// <param name="profileTitle">The profile's full title; HTML is reduced to plain text.</param>
    /// <returns>The header template HTML.</returns>
    public static string BuildHeaderHtml(string reportTitle, string profileTitle) =>
        "<div style=\"width:100%;padding:0 12.7mm;font-family:Arial,Helvetica,sans-serif;font-size:10px;text-align:center;\">" +
        $"<div style=\"border-bottom:1px solid #000;padding-bottom:4px;\">{WebUtility.HtmlEncode(reportTitle)} for:<br/>" +
        $"{WebUtility.HtmlEncode(ProfileTitleHtmlFormatter.ToPlainText(profileTitle))}</div></div>";
}
