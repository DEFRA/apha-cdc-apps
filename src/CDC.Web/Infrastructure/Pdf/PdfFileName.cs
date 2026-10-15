namespace CDC.Web.Infrastructure.Pdf;

/// <summary>Builds the file name offered when a PDF is downloaded.</summary>
public static class PdfFileName
{
    /// <summary>Builds a "{name}.pdf" file name, replacing characters that are invalid in file names.</summary>
    /// <param name="name">The name without an extension, for example "Anthrax_Summary profile".</param>
    /// <returns>A file name ending in ".pdf".</returns>
    public static string Build(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = new string([.. name.Select(character => invalid.Contains(character) || char.IsControl(character) ? '_' : character)]).Trim();

        return $"{(safeName.Length == 0 ? "Report" : safeName)}.pdf";
    }
}
