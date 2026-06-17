using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services.Themes;
using Microsoft.AspNetCore.DataProtection;

namespace Blogify.Tests;

public sealed class ThemeRegistryTests
{
    [Fact]
    public void DefaultRegistry_ExposesSelectableThemesAndFallback()
    {
        ThemeRegistry registry = new();

        Assert.Equal("default", registry.FallbackTheme.Slug);
        Assert.Equal(["default", "minimal", "aurora"], registry.SelectableThemes.Select(t => t.Slug));
    }

    [Fact]
    public void ResolveTheme_ReturnsFallbackForMissingOrDisabledThemes()
    {
        ThemeRegistry registry = new(
        [
            Theme("fallback", isFallback: true),
            Theme("retired", isEnabled: false)
        ]);

        Assert.Equal("fallback", registry.ResolveTheme("missing").Slug);
        Assert.Equal("fallback", registry.ResolveTheme("retired").Slug);
    }

    [Fact]
    public void Registry_RequiresExactlyOneFallbackTheme()
    {
        Assert.Throws<InvalidOperationException>(() => new ThemeRegistry([Theme("one"), Theme("two")]));

        Assert.Throws<InvalidOperationException>(() => new ThemeRegistry(
        [
            Theme("one", isFallback: true),
            Theme("two", isFallback: true)
        ]));
    }

    [Fact]
    public void TenantChangeTheme_UsesRegistrySelectableValidation()
    {
        ThemeRegistry registry = new(
        [
            Theme("fallback", isFallback: true),
            Theme("hidden", isSelectable: false)
        ]);
        Tenant tenant = Tenant.Create("Blog", "blog", "owner");

        tenant.ChangeTheme("fallback", registry);

        Assert.Equal("fallback", tenant.ActiveTheme);
        Assert.Throws<DomainException>(() => tenant.ChangeTheme("hidden", registry));
        Assert.Throws<DomainException>(() => tenant.ChangeTheme("missing", registry));
    }

    [Fact]
    public void ThemePreviewTokenService_ValidatesTenantAndReturnsTheme()
    {
        ThemePreviewTokenService service = new(
            new EphemeralDataProtectionProvider());
        Guid tenantId = Guid.NewGuid();

        string token = service.CreateToken(tenantId, "minimal");

        Assert.True(service.IsValidToken(token, tenantId, "minimal"));
        Assert.True(service.TryValidateToken(token, tenantId, out string? themeSlug));
        Assert.Equal("minimal", themeSlug);
        Assert.False(service.IsValidToken(token, Guid.NewGuid(), "minimal"));
        Assert.False(service.IsValidToken(token, tenantId, "aurora"));
    }

    private static BlogTheme Theme(
        string slug,
        bool isEnabled = true,
        bool isSelectable = true,
        bool isFallback = false) =>
        new(
            slug,
            $"Themes.{slug}.Name",
            $"Themes.{slug}.Description",
            $"/images/theme-previews/{slug}.png",
            slug,
            $"/css/themes/{slug}.css",
            isEnabled,
            isSelectable,
            isFallback);
}
