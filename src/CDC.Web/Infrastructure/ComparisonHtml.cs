using System.Net;
using System.Text;
using CDC.Web.Models;
using Microsoft.AspNetCore.Html;

namespace CDC.Web.Infrastructure;

/// <summary>Renders comparison segments as HTML with every text run encoded.</summary>
public static class ComparisonHtml
{
    /// <summary>Marks removed text with a strike-through and added text bold and underlined, so
    /// the change does not rely on colour alone.</summary>
    public static IHtmlContent Render(IReadOnlyList<ComparisonSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var html = new StringBuilder();

        foreach (var segment in segments)
        {
            var text = WebUtility.HtmlEncode(segment.Text);

            html.Append(segment.Change switch
            {
                ComparisonChange.Removed => $"<span class=\"removed\">{text}</span>",
                ComparisonChange.Added => $"<span class=\"added\">{text}</span>",
                _ => text
            });
        }

        return new HtmlString(html.ToString());
    }
}
