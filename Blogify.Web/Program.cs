using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Razor;
using Blogify.Web;
using Blogify.Web.Data;
using Blogify.Web.Endpoints;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Blogify.Web.Services.Email;
using Blogify.Web.Services.Themes;
using Blogify.Web.Middleware;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OutputCaching;

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
StorageOptions storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<ImageStorageProcessor>();

bool useR2Storage =
    !string.IsNullOrWhiteSpace(storageOptions.R2.AccountId) &&
    !string.IsNullOrWhiteSpace(storageOptions.R2.AccessKeyId) &&
    !string.IsNullOrWhiteSpace(storageOptions.R2.SecretAccessKey) &&
    !string.IsNullOrWhiteSpace(storageOptions.R2.BucketName) &&
    !string.IsNullOrWhiteSpace(storageOptions.R2.PublicBaseUrl);

bool hasAnyR2Settings =
    !string.IsNullOrWhiteSpace(storageOptions.R2.AccountId) ||
    !string.IsNullOrWhiteSpace(storageOptions.R2.AccessKeyId) ||
    !string.IsNullOrWhiteSpace(storageOptions.R2.SecretAccessKey) ||
    !string.IsNullOrWhiteSpace(storageOptions.R2.BucketName) ||
    !string.IsNullOrWhiteSpace(storageOptions.R2.PublicBaseUrl);

if (hasAnyR2Settings && !useR2Storage)
{
    throw new InvalidOperationException("Storage:R2 configuration is incomplete.");
}

if (useR2Storage)
{
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        StorageOptions.R2Options r2Options = storageOptions.R2;
        BasicAWSCredentials credentials = new(
            r2Options.AccessKeyId!,
            r2Options.SecretAccessKey!);

        return new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = $"https://{r2Options.AccountId}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true
        });
    });
    builder.Services.AddHttpClient<IFileStorageService, R2FileStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}
builder.Services.AddSingleton<IBlockNoteHtmlRenderer, BlockNoteHtmlRenderer>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<FeedService>();
builder.Services.AddScoped<IPublicBlogCacheInvalidator, PublicBlogCacheInvalidator>();
builder.Services.AddScoped<IBlogPermissionService, BlogPermissionService>();
builder.Services.AddScoped<IAccessibleBlogService, AccessibleBlogService>();
builder.Services.AddScoped<ILoginRedirectService, LoginRedirectService>();
builder.Services.AddScoped<BlogBrandingService>();
builder.Services.AddSingleton<IThemeRegistry>(_ => new ThemeRegistry());
builder.Services.AddSingleton<ThemePreviewTokenService>();

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

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

string[] outputCachedPublicBlogPages =
[
    "/Index",
    "/Post",
    "/Category",
    "/Tag",
    "/Categories",
    "/Tags",
    "/Archive",
    "/Archives"
];

builder.Services
    .AddRazorPages(options =>
    {
        ConfigureFriendlyPageRoutes(options.Conventions);

        foreach (string pageName in outputCachedPublicBlogPages)
        {
            options.Conventions.AddAreaPageApplicationModelConvention(
                areaName: "Blog",
                pageName: pageName,
                action: model => model.EndpointMetadata.Add(new OutputCacheAttribute
                {
                    PolicyName = PublicBlogOutputCachePolicy.PolicyName
                }));
        }
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource)));

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(PublicBlogOutputCachePolicy.PolicyName, new PublicBlogOutputCachePolicy());
});

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

app.UseOutputCache();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapCrossAuthEndpoints();
app.MapFeedEndpoints();
app.MapCultureEndpoints();
app.MapRazorPages()
   .WithStaticAssets();


app.Run();

static void ConfigureFriendlyPageRoutes(PageConventionCollection conventions)
{
    // The public Blog area keeps its existing clean subdomain routes. Platform,
    // identity, and admin surfaces use explicit replacement routes below.
    ReplacePageRoute(conventions, "/Index", "_internal/landing");
    ReplacePageRoute(conventions, "/Dashboard/Index", "dashboard");
    ReplacePageRoute(conventions, "/Dashboard/CreateBlog", "dashboard/create-blog");
    ReplacePageRoute(conventions, "/Invitation", "invite/{token}");
    ReplacePageRoute(conventions, "/Privacy", "privacy");
    ReplacePageRoute(conventions, "/MyAdmin", "my-admin");

    ReplaceAreaPageRoute(conventions, "Identity", "/Account/Login", "login");
    ReplaceAreaPageRoute(conventions, "Identity", "/Account/Register", "register");
    ReplaceAreaPageRoute(conventions, "Identity", "/Account/Logout", "logout");
    ReplaceAreaPageRoute(conventions, "Identity", "/Account/ForgotPassword", "forgot-password");
    ReplaceAreaPageRoute(conventions, "Identity", "/Account/ResetPassword", "reset-password");
    ReplaceAreaPageRoute(conventions, "Identity", "/Account/AccessDenied", "access-denied");

    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Index", "app/admin/{blogSlug}");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Posts/Index", "app/admin/{blogSlug}/posts");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Posts/Create", "app/admin/{blogSlug}/posts/new");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Posts/Edit", "app/admin/{blogSlug}/posts/{id:guid}/edit");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Media/Index", "app/admin/{blogSlug}/media");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Analytics/Index", "app/admin/{blogSlug}/analytics");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Comments/Index", "app/admin/{blogSlug}/comments");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Themes/Index", "app/admin/{blogSlug}/themes");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Categories/Index", "app/admin/{blogSlug}/categories");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Categories/Create", "app/admin/{blogSlug}/categories/new");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Categories/Edit", "app/admin/{blogSlug}/categories/{id:guid}/edit");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Tags/Index", "app/admin/{blogSlug}/tags");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Tags/Create", "app/admin/{blogSlug}/tags/new");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Tags/Edit", "app/admin/{blogSlug}/tags/{id:guid}/edit");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Members/Index", "app/admin/{blogSlug}/members");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Members/Invite", "app/admin/{blogSlug}/members/invite");
    ReplaceAreaPageRoute(conventions, "BlogAdmin", "/Settings/Index", "app/admin/{blogSlug}/settings");

    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Index", "sa");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Users/Index", "sa/users");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Users/Create", "sa/users/new");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Users/Edit", "sa/users/{id}/edit");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Users/Delete", "sa/users/{id}/delete");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Blogs/Index", "sa/blogs");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Blogs/Create", "sa/blogs/new");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Blogs/Edit", "sa/blogs/{id:guid}/edit");
    ReplaceAreaPageRoute(conventions, "SuperAdmin", "/Blogs/Delete", "sa/blogs/{id:guid}/delete");
}

static void ReplacePageRoute(PageConventionCollection conventions, string pageName, string routeTemplate)
{
    conventions.AddPageRouteModelConvention(pageName, model => ReplaceRoute(model, routeTemplate));
}

static void ReplaceAreaPageRoute(
    PageConventionCollection conventions,
    string areaName,
    string pageName,
    string routeTemplate)
{
    conventions.AddAreaPageRouteModelConvention(
        areaName,
        pageName,
        model => ReplaceRoute(model, routeTemplate));
}

static void ReplaceRoute(PageRouteModel model, string routeTemplate)
{
    model.Selectors.Clear();
    model.Selectors.Add(new SelectorModel
    {
        AttributeRouteModel = new AttributeRouteModel
        {
            Template = routeTemplate
        }
    });
}
