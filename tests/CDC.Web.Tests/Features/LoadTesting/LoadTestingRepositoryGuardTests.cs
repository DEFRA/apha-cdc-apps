using System.Text.Json;

namespace CDC.Web.Tests.Features.LoadTesting;

// LoadTesting bypasses sign-in, so only an environment's own configuration may switch it on - never anything committed to the repo.
public class LoadTestingRepositoryGuardTests
{
    private static readonly string? Root = FindRepositoryRoot();

    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CDC.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    [Theory]
    [InlineData("src/CDC.Web/appsettings.json")]
    [InlineData("src/CDC.Api/appsettings.json")]
    public void NoCommittedAppSettingsSwitchLoadTestingOn(string relativePath)
    {
        if (Root is null)
        {
            return;
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root, relativePath)),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var enabled = document.RootElement.TryGetProperty("LoadTesting", out var section)
            && section.TryGetProperty("Enabled", out var value)
            && string.Equals(value.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase);

        Assert.False(enabled, $"{relativePath} must not switch LoadTesting on.");
    }

    [Fact]
    public void NoDockerfileWorkflowOrLaunchProfileMentionsLoadTesting()
    {
        if (Root is null)
        {
            return;
        }

        var files = Directory.EnumerateFiles(Path.Combine(Root, ".github"), "*.yml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "src"), "Dockerfile", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "src"), "launchSettings.json", SearchOption.AllDirectories))
            .ToList();

        Assert.NotEmpty(files);
        Assert.DoesNotContain(files, file => File.ReadAllText(file).Contains("LoadTesting", StringComparison.OrdinalIgnoreCase));
    }
}
