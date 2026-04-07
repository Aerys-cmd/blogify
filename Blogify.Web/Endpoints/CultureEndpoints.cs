using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Blogify.Web.Endpoints;

public static class CultureEndpoints
{
    public static IEndpointRouteBuilder MapCultureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/culture", async (
            HttpContext context,
            IAntiforgery antiforgery,
            [FromForm] string? culture,
            [FromForm] string? redirectUri) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Invalid request.");
            }

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
