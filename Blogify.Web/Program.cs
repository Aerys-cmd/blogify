using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Razor;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Blogify.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<ApplicationDbContext>("blogdb");

builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<DatabaseMigrator>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationExpanders.Add(new ThemeViewLocationExpander());
});

// Add services to the container.
builder.Services.AddRazorPages(options =>
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
});

var app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

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

app.UseAuthorization();


app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
