namespace Blogify.Web.Endpoints;

/// <summary>
/// The cross-subdomain authentication handshake has been removed.
/// Authentication now works via a single cookie on the platform domain.
/// Admin access is available at /app/admin/{blogSlug} on the root domain — no cross-domain sign-in required.
/// This file is retained as an empty stub; the route is no longer registered.
/// </summary>
public static class CrossAuthEndpoints
{
    public static IEndpointRouteBuilder MapCrossAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Cross-auth removed. Admin lives on the root domain at /app/admin/{blogSlug}.
        return app;
    }
}
