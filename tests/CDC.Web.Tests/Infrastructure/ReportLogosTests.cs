using System.Buffers.Binary;
using CDC.Web.Infrastructure.Pdf;

namespace CDC.Web.Tests.Infrastructure;

public class ReportLogosTests
{
    private const string DataUriPrefix = "data:image/png;base64,";

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void AphaLogo_IsTheLegacyStackedLogo_AsAPngDataUri() =>
        AssertPng(ReportLogos.AphaLogoDataUri, expectedWidth: 125, expectedHeight: 108);

    [Fact]
    public void D2R2Logo_IsTheLegacyLogo_AsAPngDataUri() =>
        AssertPng(ReportLogos.D2R2LogoDataUri, expectedWidth: 1418, expectedHeight: 1189);

    [Fact]
    public void Logos_AreReadOnce_SoEveryReportGetsTheSameString()
    {
        Assert.Same(ReportLogos.AphaLogoDataUri, ReportLogos.AphaLogoDataUri);
        Assert.Same(ReportLogos.D2R2LogoDataUri, ReportLogos.D2R2LogoDataUri);
    }

    private static void AssertPng(string dataUri, int expectedWidth, int expectedHeight)
    {
        Assert.StartsWith(DataUriPrefix, dataUri, StringComparison.Ordinal);
        var bytes = Convert.FromBase64String(dataUri[DataUriPrefix.Length..]);

        Assert.Equal(PngSignature, bytes[..8]);
        // The IHDR chunk follows the signature: its width and height are the first two big-endian integers of its data.
        Assert.Equal(expectedWidth, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.Equal(expectedHeight, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }
}
