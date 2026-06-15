using System.Globalization;
using Blogify.Web.Middleware;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Blogify.Tests;

public sealed class TenantLanguageTests
{
    [Fact]
    public void Create_DefaultsPublicLanguageToTurkish()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        Assert.Equal("tr", tenant.PublicLanguage);
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData(" TR ", "tr")]
    public void ChangePublicLanguage_NormalizesSupportedLanguage(string input, string expected)
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        tenant.ChangePublicLanguage(input);

        Assert.Equal(expected, tenant.PublicLanguage);
    }

    [Fact]
    public void ChangePublicLanguage_RejectsUnsupportedLanguage()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        Assert.Throws<DomainException>(() => tenant.ChangePublicLanguage("de"));
    }

    [Fact]
    public async Task PublicBlogCulture_UsesTenantLanguage()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");
        tenant.ChangePublicLanguage("tr");
        TenantContext tenantContext = new();
        tenantContext.Resolve(tenant);

        DefaultHttpContext httpContext = new();
        httpContext.Request.RouteValues["area"] = "Blog";
        string? observedCulture = null;
        PublicBlogCultureMiddleware middleware = new(_ =>
        {
            observedCulture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        Assert.Equal("tr", observedCulture);
    }

    [Fact]
    public async Task PublicBlogCulture_DoesNotOverrideAdminCulture()
    {
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");
        TenantContext tenantContext = new();
        tenantContext.Resolve(tenant);

        DefaultHttpContext httpContext = new();
        httpContext.Request.RouteValues["area"] = "BlogAdmin";
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        string? observedCulture = null;
        PublicBlogCultureMiddleware middleware = new(_ =>
        {
            observedCulture = CultureInfo.CurrentUICulture.Name;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        Assert.Equal(originalCulture.Name, observedCulture);
    }
}
