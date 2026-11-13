using System.Text;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace CDC.Web.Tests.Integration;

// TEMPORARY real-browser check (delete after use): the clone "what-if" scenario page in Edge.
public class TempScenarioPageBrowserCheck(ITestOutputHelper output)
{
    private static readonly Guid CloneVersionId = Guid.NewGuid();
    private static readonly Guid Animals = Guid.NewGuid();
    private static readonly Guid Cattle = Guid.NewGuid();
    private static readonly Guid Sheep = Guid.NewGuid();
    private static readonly Guid Goats = Guid.NewGuid();

    private static readonly SpeciesDto[] Species =
    [
        new() { Id = Animals, ParentId = Guid.Empty, Description = "Animals", IsActive = true },
        new() { Id = Cattle, ParentId = Animals, Description = "Cattle", IsActive = true },
        new() { Id = Sheep, ParentId = Animals, Description = "Sheep", IsActive = true },
        new() { Id = Goats, ParentId = Animals, Description = "Goats", IsActive = false }
    ];

    private static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Wwwroot(string relative) => File.ReadAllText(Path.Combine(Root, "src", "CDC.Web", "wwwroot", relative));

    [Fact]
    public async Task ScenarioPage_InARealBrowser()
    {
        var properties = new FakeProfilePropertiesApiService(defaults: new NewProfileDefaultsDto
        {
            ScenarioTitle = string.Empty,
            ParentTitle = "Anthrax",
            ClonedVersionDescription = "Anthrax - v2.0 (Published) Valid from 01/01/2020"
        });

        using var factory = new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient());
                services.RemoveAll<IProfilePropertiesApiService>();
                services.AddSingleton<IProfilePropertiesApiService>(properties);
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService(Species));
            }));
        var client = await factory.SignedInClientAsync();
        var html = await client.GetStringAsync($"/SurveillanceProfiles/ProfileProperties?cloneProfileVersionId={CloneVersionId}&scenario=1");

        html = MakeStandalone(html);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge", Headless = true });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1280, Height = 1250 } });
        await page.SetContentAsync(html);

        var shots = Path.Combine(Path.GetTempPath(), "scenario-shots");
        Directory.CreateDirectory(shots);

        async Task State(string name)
        {
            var json = await page.EvaluateAsync<string>(StateScript);
            output.WriteLine($"[{name}] {json}");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(shots, $"{name}.png"), FullPage = true });
        }

        await State("0-open");

        await page.GetByRole(AriaRole.Button, new() { Name = "Reset selections" }).ClickAsync();
        await State("1-after-reset");

        await page.Locator("label", new PageLocatorOptions { HasTextString = "Cattle" }).First.ClickAsync();
        await State("2-profiled-ticked");

        await page.Locator("#ProfileTitleEditor").ClickAsync();
        await page.Keyboard.TypeAsync("Anthrax");
        await State("3-typed-title");
        await page.Keyboard.PressAsync("Control+A");
        await page.Keyboard.PressAsync("Backspace");
        await State("4-title-deleted-before-click-away");

        await page.Locator("h1").ClickAsync();
        await State("5-after-click-away");

        await page.Locator("label", new PageLocatorOptions { HasTextString = "Cattle" }).First.ClickAsync();
        await State("6-profiled-unticked");

        await page.GetByRole(AriaRole.Button, new() { Name = "Reset selections" }).ClickAsync();
        await State("7-reset-again");
    }

    private const string StateScript = """
        () => {
          const visible = (el) => !!el && !el.hidden && getComputedStyle(el).display !== 'none' && getComputedStyle(el).visibility !== 'hidden';
          const q = (s) => document.querySelector(s);
          const all = (s) => [...document.querySelectorAll(s)];
          const text = (el) => el ? el.textContent.replace(/\s+/g, ' ').trim() : null;
          const marks = all('[data-app-title-required-mark], [data-species-required-mark]').map((el) => ({ id: el.dataset.appTitleRequiredMark !== undefined ? 'title' : 'species', visible: visible(el), text: text(el) }));
          const status = (kind) => { const el = q(`[data-species-status="${kind}"]`); return el ? { visible: visible(el), text: text(el), weight: getComputedStyle(el).fontWeight, strongWeight: el.querySelector('strong') ? getComputedStyle(el.querySelector('strong')).fontWeight : null } : null; };
          return JSON.stringify({
            saveVisible: visible(q('[data-app-title-save]')),
            titleRequiredMessage: { visible: visible(q('[data-app-title-required-error]')), text: text(q('[data-app-title-required-error]')) },
            marks,
            profiledStatus: status('profiled'),
            otherStatus: status('other'),
            titleHtml: q('#ProfileTitleEditor').innerHTML
          });
        }
        """;

    // A page that works without the layout's scripts: every stylesheet and the page's own scripts are inlined.
    private static string MakeStandalone(string html)
    {
        html = Regex.Replace(html, "<link rel=\"stylesheet\" href=\"/css/(govuk-frontend-[^\"?]+)[^\"]*\" ?/?>", m => $"<style>{Wwwroot("css/" + m.Groups[1].Value)}</style>", RegexOptions.None, TimeSpan.FromSeconds(10));
        html = Regex.Replace(html, "<link rel=\"stylesheet\" href=\"/css/site\\.css[^\"]*\" ?/?>", _ => $"<style>{Wwwroot("css/site.css")}</style>", RegexOptions.None, TimeSpan.FromSeconds(10));
        html = Regex.Replace(html, "<link rel=\"stylesheet\" href=\"/lib/bootstrap/dist/css/bootstrap\\.min\\.css[^\"]*\" ?/?>", _ => $"<style>{Wwwroot("lib/bootstrap/dist/css/bootstrap.min.css")}</style>", RegexOptions.None, TimeSpan.FromSeconds(10));

        var keep = new[] { "js-enabled" };
        html = Regex.Replace(html, "<script\\b[^>]*>[\\s\\S]*?</script>", m => keep.Any(m.Value.Contains) ? m.Value : string.Empty, RegexOptions.None, TimeSpan.FromSeconds(10));

        var scripts = new StringBuilder();
        foreach (var file in new[] { "tree-view.js", "edit-profile-title.js", "profile-properties.js" })
        {
            scripts.Append("<script>").Append(Wwwroot("js/" + file)).Append("</script>");
        }

        return html.Replace("</body>", scripts + "</body>", StringComparison.Ordinal);
    }
}
