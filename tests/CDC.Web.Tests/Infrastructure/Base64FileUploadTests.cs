using System.Text;
using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.Infrastructure;

public class Base64FileUploadTests
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4 <x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF/></x:xmpmeta>");

    private static FormFile CreateFile(byte[] content, string contentType, long? length = null) =>
        new(new MemoryStream(content), 0, length ?? content.Length, "File", "report.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

    private static FormFile CreateBase64File(byte[] content) =>
        CreateFile(Encoding.ASCII.GetBytes(Convert.ToBase64String(content)), Base64FileUpload.ContentType);

    [Fact]
    public async Task ReadAsync_ReturnsTheBytesUnchanged_WhenTheFileIsNotMarkedAsBase64()
    {
        var result = await Base64FileUpload.ReadAsync(CreateFile(Pdf, "application/pdf"), CancellationToken.None);

        Assert.Equal(Pdf, result);
    }

    [Fact]
    public async Task ReadAsync_DoesNotDecode_WhenTheContentIsBase64ButNotMarked()
    {
        var text = Encoding.ASCII.GetBytes(Convert.ToBase64String(Pdf));

        var result = await Base64FileUpload.ReadAsync(CreateFile(text, "application/pdf"), CancellationToken.None);

        Assert.Equal(text, result);
    }

    [Theory]
    [InlineData("application/base64")]
    [InlineData("APPLICATION/BASE64")]
    public async Task ReadAsync_DecodesTheContent_WhenTheFileIsMarkedAsBase64(string contentType)
    {
        var text = Encoding.ASCII.GetBytes(Convert.ToBase64String(Pdf));

        var result = await Base64FileUpload.ReadAsync(CreateFile(text, contentType), CancellationToken.None);

        Assert.Equal(Pdf, result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ReadAsync_DecodesContentWhoseLengthIsNotAMultipleOfThree(int length)
    {
        var content = new byte[length];
        new Random(length).NextBytes(content);

        var result = await Base64FileUpload.ReadAsync(CreateBase64File(content), CancellationToken.None);

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ReadAsync_RestoresEveryByteOfLargeBinaryContent()
    {
        var content = new byte[600_000];
        new Random(42).NextBytes(content);
        for (var value = 0; value < 256; value++)
        {
            content[value] = (byte)value;
        }

        var result = await Base64FileUpload.ReadAsync(CreateBase64File(content), CancellationToken.None);

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task ReadAsync_ReturnsAnEmptyArray_WhenTheMarkedFileIsEmpty()
    {
        var result = await Base64FileUpload.ReadAsync(CreateFile([], Base64FileUpload.ContentType), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("JVBERi0xLjQ")]
    public async Task ReadAsync_ReturnsNull_WhenTheMarkedFileIsNotValidBase64(string text)
    {
        var file = CreateFile(Encoding.ASCII.GetBytes(text), Base64FileUpload.ContentType);

        Assert.Null(await Base64FileUpload.ReadAsync(file, CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_IgnoresLineBreaksInTheBase64Text()
    {
        var file = CreateFile(Encoding.ASCII.GetBytes("JVBERi0x\r\nLjQ="), Base64FileUpload.ContentType);

        Assert.Equal("%PDF-1.4"u8.ToArray(), await Base64FileUpload.ReadAsync(file, CancellationToken.None));
    }

    [Theory]
    [InlineData("application/pdf", 100, 100, false)]
    [InlineData("application/pdf", 101, 100, true)]
    [InlineData("application/base64", 136, 100, false)]
    [InlineData("application/base64", 137, 100, true)]
    public void ExceedsLimit_AllowsBase64ToBeAThirdLarger(string contentType, int length, int maxBytes, bool expected)
    {
        var file = CreateFile([], contentType, length);

        Assert.Equal(expected, Base64FileUpload.ExceedsLimit(file, maxBytes));
    }
}
