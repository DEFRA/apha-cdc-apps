using System.Net;
using System.Text;

namespace CDC.Common.Comparison;

/// <summary>Renders comparison reports as one self-contained HTML document (inline styles, no network access) for PDF conversion.</summary>
public static class ProfileVersionComparisonHtml
{
    private const string Styles =
        "body{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#0b0c0c;margin:0}" +
        "h1{font-size:16px;margin:0 0 10px}h2{font-size:13px;margin:14px 0 4px}" +
        "table{width:100%;border-collapse:collapse;table-layout:fixed;margin-bottom:6px}thead{display:table-header-group}" +
        "th,td{border:1px solid #b1b4b6;padding:4px 6px;vertical-align:top;text-align:left;word-wrap:break-word;white-space:pre-wrap}" +
        "th{background:#f3f2f1;white-space:normal}tr{page-break-inside:avoid}" +
        ".question td,.group td{background:#e8e8e8;font-weight:bold;white-space:normal}" +
        ".group td{background:#f3f2f1;font-weight:normal;font-style:italic}" +
        ".removed{color:#d4351c;text-decoration:line-through}.added{color:#005a30;font-weight:bold;text-decoration:underline}" +
        ".key{margin:0 0 8px}.identical,.none{text-align:center;font-style:italic}.identical{margin:4px 0 10px}.report+.report{page-break-before:always}";

    /// <summary>Renders each report in turn, starting every report after the first on a new page.</summary>
    /// <param name="reports">The reports to render.</param>
    public static string Render(IReadOnlyList<ProfileVersionComparisonReport> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" /><title>Profile version comparison</title><style>")
            .Append(Styles).Append("</style></head><body>");

        foreach (var report in reports)
        {
            AppendReport(html, report);
        }

        return html.Append("</body></html>").ToString();
    }

    private static void AppendReport(StringBuilder html, ProfileVersionComparisonReport report)
    {
        html.Append("<div class=\"report\"><h1>Profile version comparison</h1>")
            .Append("<p class=\"key\"><span class=\"removed\">Struck-through text</span> was removed. <span class=\"added\">Bold underlined text</span> was added.</p>");

        if (report.Sections.All(section => section.IsIdentical))
        {
            html.Append("<p class=\"none\">The two profile versions are identical.</p>");
        }

        foreach (var section in report.Sections)
        {
            html.Append("<h2>").Append(Encode(section.Heading)).Append("</h2>");

            if (section.IsIdentical)
            {
                html.Append("<p class=\"identical\">- Sections are identical -</p>");
                continue;
            }

            html.Append("<table><thead><tr><th scope=\"col\" style=\"width:20%\">Question</th><th scope=\"col\" style=\"width:40%\">")
                .Append(Encode(report.LeftTitle)).Append("</th><th scope=\"col\" style=\"width:40%\">")
                .Append(Encode(report.RightTitle)).Append("</th></tr></thead><tbody>");

            foreach (var question in section.Questions)
            {
                AppendQuestion(html, question);
            }

            html.Append("</tbody></table>");
        }

        html.Append("</div>");
    }

    private static void AppendQuestion(StringBuilder html, ComparisonQuestion question)
    {
        html.Append("<tr class=\"question\"><td colspan=\"3\">").Append(Encode(question.Number)).Append(' ').Append(Encode(question.Text)).Append("</td></tr>");

        foreach (var group in question.Groups)
        {
            if (group.Heading is not null)
            {
                html.Append("<tr class=\"group\"><td colspan=\"3\">").Append(Encode(group.Heading)).Append("</td></tr>");
            }

            foreach (var field in group.Fields)
            {
                html.Append("<tr><td>").Append(Encode(field.Label)).Append("</td><td>");
                AppendSegments(html, field.Left);
                html.Append("</td><td>");
                AppendSegments(html, field.Right);
                html.Append("</td></tr>");
            }
        }
    }

    private static void AppendSegments(StringBuilder html, IReadOnlyList<ComparisonSegment> segments)
    {
        foreach (var segment in segments)
        {
            var text = Encode(segment.Text);

            html.Append(segment.Change switch
            {
                ComparisonChange.Removed => $"<span class=\"removed\">{text}</span>",
                ComparisonChange.Added => $"<span class=\"added\">{text}</span>",
                _ => text
            });
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
