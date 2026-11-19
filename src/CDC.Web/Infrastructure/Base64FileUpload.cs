using System.Buffers;
using System.Buffers.Text;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Reads an uploaded file that <c>base64-file-upload.js</c> may have sent as base64 text, which it marks with
/// <see cref="ContentType"/>, so the WAF in front of the site never has to scan raw PDF bytes for cross-site scripting.
/// A file without the marker (JavaScript disabled) is read unchanged.
/// </summary>
public static class Base64FileUpload
{
    /// <summary>The content type <c>base64-file-upload.js</c> gives a file whose content it has base64-encoded.</summary>
    public const string ContentType = "application/base64";

    /// <summary>Gets whether the largest file the caller accepts is exceeded, allowing for base64's one-third growth.</summary>
    public static bool ExceedsLimit(IFormFile file, int maxBytes) =>
        file.Length > (IsBase64Encoded(file) ? Base64.GetMaxEncodedToUtf8Length(maxBytes) : maxBytes);

    /// <summary>Reads the file's content, decoding it when marked as base64.</summary>
    /// <returns>The content, or <see langword="null"/> when the file is marked as base64 but is not valid base64.</returns>
    public static async Task<byte[]?> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        if (!IsBase64Encoded(file))
        {
            return stream.ToArray();
        }

        // Decoding in place reuses the buffer holding the text rather than allocating a second large one.
        var buffer = stream.GetBuffer().AsSpan(0, (int)stream.Length);
        var status = Base64.DecodeFromUtf8InPlace(buffer, out var written);

        return status == OperationStatus.Done ? buffer[..written].ToArray() : null;
    }

    private static bool IsBase64Encoded(IFormFile file) =>
        string.Equals(file.ContentType, ContentType, StringComparison.OrdinalIgnoreCase);
}
