using Microsoft.AspNetCore.Localization;

namespace Blogify.Web.Endpoints;

public static class CultureEndpoints
{
    public static IEndpointRouteBuilder MapCultureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/culture", (HttpContext context, string? culture, string? redirectUri) =>
        {
            if (string.IsNullOrEmpty(culture) ||
                (!string.Equals(culture, "en", StringComparison.Ordinal) &&
                 !string.Equals(culture, "tr", StringComparison.Ordinal)))
            {
                return Results.BadRequest("Unsupported culture.");
            }

            string cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));

            context.Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                cookieValue,
                new CookieOptions
                {
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    HttpOnly = true
                });

            string destination = string.IsNullOrEmpty(redirectUri) ? "/" : redirectUri;
            return Results.LocalRedirect(destination);
        });

        return app;
    }
}
