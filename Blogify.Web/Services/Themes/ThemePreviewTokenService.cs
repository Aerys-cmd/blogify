using Microsoft.AspNetCore.DataProtection;

namespace Blogify.Web.Services.Themes;

public sealed class ThemePreviewTokenService
{
    public const string ThemeQueryKey = "previewTheme";
    public const string TokenQueryKey = "previewToken";
    public const string ExitQueryKey = "exitThemePreview";
    public const string CookieName = ".Blogify.ThemePreview";
    public static readonly TimeSpan PreviewDuration = TimeSpan.FromMinutes(30);

    private readonly IDataProtector _protector;

    public ThemePreviewTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("Blogify.ThemePreview.v1");
    }

    public string CreateToken(Guid tenantId, string themeSlug)
    {
        string payload = $"{tenantId:N}|{themeSlug}|{DateTimeOffset.UtcNow.Add(PreviewDuration).ToUnixTimeSeconds()}";
        return _protector.Protect(payload);
    }

    public bool IsValidToken(string? token, Guid tenantId, string themeSlug)
    {
        return TryValidateToken(token, tenantId, out string? validatedThemeSlug) &&
            string.Equals(validatedThemeSlug, themeSlug, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryValidateToken(string? token, Guid tenantId, out string? themeSlug)
    {
        themeSlug = null;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            string payload = _protector.Unprotect(token);
            string[] parts = payload.Split('|');
            if (parts.Length != 3)
                return false;

            if (!Guid.TryParseExact(parts[0], "N", out Guid tokenTenantId) || tokenTenantId != tenantId)
                return false;

            if (!long.TryParse(parts[2], out long expiresAt) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
            {
                return false;
            }

            themeSlug = parts[1];
            return true;
        }
        catch
        {
            return false;
        }
    }
}
