using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Endpoints;

/// <summary>
/// Handles blog invitation acceptance at GET /invite/{token}.
/// If the user is authenticated, creates the membership and redirects to the blog admin.
/// If the user is not authenticated, redirects to login/register with the invitation token preserved.
/// </summary>
public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/invite/{token}", async (
            string token,
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest("Invalid invitation token.");

            BlogInvitation? invitation = await dbContext.BlogInvitations
                .FirstOrDefaultAsync(i => i.Token == token);

            if (invitation is null || !invitation.IsValid)
                return Results.BadRequest("This invitation is invalid or has expired.");

            // If not authenticated, send to register/login and come back after.
            if (context.User.Identity?.IsAuthenticated != true)
            {
                string returnUrl = $"/invite/{token}";
                return Results.Redirect($"/Identity/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            ApplicationUser? user = await userManager.GetUserAsync(context.User);
            if (user is null)
                return Results.Redirect($"/Identity/Account/Login?returnUrl={Uri.EscapeDataString($"/invite/{token}")}");

            // Make sure the invitation email matches the logged-in user's email.
            if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("This invitation was sent to a different email address. Please sign in with that email.");

            // Check if already a member.
            bool alreadyMember = await dbContext.BlogMemberships
                .AnyAsync(m => m.BlogId == invitation.BlogId && m.UserId == user.Id);

            if (!alreadyMember)
            {
                BlogMembership membership = BlogMembership.Create(
                    invitation.BlogId, user.Id, invitation.Role, invitation.InvitedByUserId);
                dbContext.BlogMemberships.Add(membership);
            }

            invitation.Accept();
            await dbContext.SaveChangesAsync();

            // Resolve blog slug for redirect.
            string? blogSlug = await dbContext.Blogs
                .AsNoTracking()
                .Where(b => b.Id == invitation.BlogId)
                .Select(b => b.Subdomain)
                .FirstOrDefaultAsync();

            return blogSlug is not null
                ? Results.Redirect($"/app/admin/{blogSlug}")
                : Results.Redirect("/dashboard");
        });

        return app;
    }
}
