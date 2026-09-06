using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
// Also enable Razor Pages (some projects in the solution use Razor Pages)
builder.Services.AddRazorPages();

// Allow views to be located under Features/{Controller}/Views and Features/Shared
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Insert(0, "/Features/{1}/Views/{0}.cshtml");
    options.ViewLocationFormats.Insert(1, "/Features/Shared/{0}.cshtml");
});

// NOTE: real authentication (Entra ID SAML for internal users, CIDM/GOV.UK One Login OIDC for
// external users) is not wired up yet. It will replace this placeholder Landing selection screen.

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Landing/Error");
}

// Serve static files from wwwroot
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Landing}/{action=Index}/{id?}")
    .WithStaticAssets();

// Ensure Razor Pages are available if any exist in the project
app.MapRazorPages();

app.Run();
