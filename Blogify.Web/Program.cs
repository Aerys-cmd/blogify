using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Razor;
using Blogify.Web;
using Blogify.Web.Data;
using Blogify.Web.Endpoints;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Blogify.Web.Middleware;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.HttpOverrides;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<ApplicationDbContext>("blogdb");

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<DatabaseMigrator>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<FeedService>();

builder.Services.Configure<AnalyticsOptions>(builder.Configuration.GetSection("Analytics"));
builder.Services.AddSingleton<AnalyticsChannel>();
builder.Services.AddHostedService<AnalyticsWriterService>();

builder.Services.Configure<TenantOptions>(
    builder.Configuration.GetSection("Tenant"));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    // Trust only RFC-1918 private ranges used by Docker internal networks.
    // This restricts header spoofing to hosts inside those private ranges
    // while still supporting any Docker Compose subnet assignment.
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
});

string dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? (builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "keys")
        : "/app/keys");

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("Blogify");

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationExpanders.Add(new ThemeViewLocationExpander());
});

// Add services to the container.
builder.Services
    .AddRazorPages(options =>
    {
    // Map the BlogAdmin area under the /admin route prefix instead of /BlogAdmin.
    options.Conventions.AddAreaFolderRouteModelConvention(
        areaName: "BlogAdmin",
        folderPath: "/",
        action: model =>
        {
            const string areaPrefix = "BlogAdmin";
            const string newPrefix = "admin";

            foreach (SelectorModel selector in model.Selectors)
            {
                if (selector.AttributeRouteModel?.Template is not string template)
                    continue;

                if (string.Equals(template, areaPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    selector.AttributeRouteModel.Template = newPrefix;
                }
                else if (template.StartsWith(areaPrefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    selector.AttributeRouteModel.Template = newPrefix + template[areaPrefix.Length..];
                }
            }
        });

    // Map the SuperAdmin area under the /sa route prefix instead of /SuperAdmin.
    options.Conventions.AddAreaFolderRouteModelConvention(
        areaName: "SuperAdmin",
        folderPath: "/",
        action: model =>
        {
            const string areaPrefix = "SuperAdmin";
            const string newPrefix = "sa";

            foreach (SelectorModel selector in model.Selectors)
            {
                if (selector.AttributeRouteModel?.Template is not string template)
                    continue;

                if (string.Equals(template, areaPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    selector.AttributeRouteModel.Template = newPrefix;
                }
                else if (template.StartsWith(areaPrefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    selector.AttributeRouteModel.Template = newPrefix + template[areaPrefix.Length..];
                }
            }
        });
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource)));

var app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.MapDefaultEndpoints();

// UseForwardedHeaders must be first so that scheme/host/IP are correctly set
// before any downstream middleware (HSTS, HTTPS redirection, auth callbacks, etc.)
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

string[] supportedCultures = ["en", "tr"];
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseStaticFiles();

// Explicit routing must be first so that route data is available to all subsequent middleware.
app.UseRouting();

app.UseAuthentication();

// Tenant resolution must run after authentication (user identity is set) but before
// authorization so that downstream middleware and pages can access TenantContext.
app.UseTenantResolution();

// Landing page access guard: enforces host-based routing for root Pages.
// Root pages (no area) are only accessible on the root domain; tenant subdomain
// requests to them return 404. Also redirects Blog area hits on the root domain
// (caused by Areas/Blog/Pages/Index.cshtml using @page "/") to the landing page.
// Must run after tenant resolution and before Blog/BlogAdmin access guards.
app.UseLandingAccess();

// BlogAdmin access guard: validates tenant presence and ownership/membership for
// requests targeting the BlogAdmin area. Runs after tenant resolution and before
// the ASP.NET Core authorization middleware.
app.UseBlogAdminAccess();

// Blog access guard: redirects unresolved-tenant requests for the Blog area to
// the appropriate destination based on the user's authentication state and role.
// Runs after tenant resolution and before the ASP.NET Core authorization middleware.
app.UseBlogAccess();

// Analytics tracking: fire-and-forget page view recording for the Blog area.
// Runs after access guards so only legitimate tenant page views are tracked.
app.UseAnalyticsTracking();

app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapCrossAuthEndpoints();
app.MapFeedEndpoints();
app.MapCultureEndpoints();

app.MapRazorPages()
   .WithStaticAssets();


app.Run();
