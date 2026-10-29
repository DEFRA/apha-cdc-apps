namespace CDC.Web.Infrastructure.Pdf;

/// <summary>
/// The two logos the legacy reports print in the header of their first page: the Animal and Plant Health Agency crest and
/// name, and the D2R2 logo. They are the legacy images, embedded in the assembly and returned as data URIs because the
/// report HTML is printed from memory, where a relative image address would not resolve.
/// </summary>
public static class ReportLogos
{
    private const string AphaLogoResource = "CDC.Web.Pdf.aphalogo.png";
    private const string D2R2LogoResource = "CDC.Web.Pdf.D2R2LogoSmall.png";

    private static readonly Lazy<string> AphaLogo = new(() => ReadDataUri(AphaLogoResource));
    private static readonly Lazy<string> D2R2Logo = new(() => ReadDataUri(D2R2LogoResource));

    /// <summary>Gets the Animal and Plant Health Agency logo as a <c>data:image/png</c> URI.</summary>
    public static string AphaLogoDataUri => AphaLogo.Value;

    /// <summary>Gets the D2R2 logo as a <c>data:image/png</c> URI.</summary>
    public static string D2R2LogoDataUri => D2R2Logo.Value;

    private static string ReadDataUri(string resourceName)
    {
        using var stream = typeof(ReportLogos).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded logo '{resourceName}' is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return $"data:image/png;base64,{Convert.ToBase64String(buffer.ToArray())}";
    }
}
