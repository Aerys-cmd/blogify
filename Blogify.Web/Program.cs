using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Razor;
using Blogify.Web;
using Blogify.Web.Data;
using Blogify.Web.Endpoints;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Blogify.Web.Services.Email;
using Blogify.Web.Middleware;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

string connectionString = builder.Configuration.GetConnectionString("blogdb")
    ?? throw new InvalidOperationException("Connection string 'blogdb' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

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
builder.Services.AddSingleton<IBlockNoteHtmlRenderer, BlockNoteHtmlRenderer>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<FeedService>();
builder.Services.AddScoped<IBlogPermissionService, BlogPermissionService>();
builder.Services.AddScoped<IAccessibleBlogService, AccessibleBlogService>();
builder.Services.AddScoped<ILoginRedirectService, LoginRedirectService>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddSingleton<IEmailQueue>(services => services.GetRequiredService<EmailQueue>());
builder.Services.AddScoped<IRazorEmailRenderer, RazorEmailRenderer>();
builder.Services.AddScoped<IAppEmailSender, AppEmailSender>();
builder.Services.AddSingleton<IEmailDeliveryTransport>(services =>
    builder.Configuration.GetValue<bool>("Email:Enabled")
        ? ActivatorUtilities.CreateInstance<SmtpEmailDeliveryTransport>(services)
        : ActivatorUtilities.CreateInstance<DisabledEmailDeliveryTransport>(services));
builder.Services.AddHostedService<EmailDispatchWorker>();

builder.Services.Configure<AnalyticsOptions>(builder.Configuration.GetSection("Analytics"));
builder.Services.AddSingleton<AnalyticsChannel>();
builder.Services.AddHostedService<AnalyticsWriterService>();

builder.Services.Configure<FeedbackHubOptions>(
    builder.Configuration.GetSection("FeedbackHub"));

builder.Services.Configure<TenantOptions>(
    builder.Configuration.GetSection("Tenant"));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
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

builder.Services
    .AddRazorPages(options =>
    {
    // Map the BlogAdmin area under /app/admin/{blogSlug} so the blog is identified
    // by route parameter rather than subdomain, enabling a single-auth architecture
    // where all admin pages live on the root domain cookie.
    options.Conventions.AddAreaFolderRouteModelConvention(
        areaName: "BlogAdmin",
        folderPath: "/",
        action: model =>
        {
            const string areaPrefix = "BlogAdmin";
            const string newPrefix = "app/admin/{blogSlug}";

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
                    selector.AttributeRouteModel.Template = newPrefix + "/" + template[(areaPrefix.Length + 1)..];
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

app.UseForwardedHeaders();

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

app.UseRouting();

app.UseAuthentication();

// Tenant resolution must run after authentication but before authorization.
app.UseTenantResolution();

// Public blogs use the language selected by their owner, not the visitor culture cookie.
app.UsePublicBlogCulture();

// Unified access control: host-based routing + blog ownership/membership checks.
app.UseAccessControl();

// Analytics tracking: fire-and-forget page view recording for the Blog area.
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
