using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Pages;

[Authorize]
public sealed class InvitationModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public string Token { get; private set; } = string.Empty;
    public string BlogTitle { get; private set; } = string.Empty;
    public BlogRole Role { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken ct = default)
    {
        Token = token;
        BlogInvitation? invitation = await FindInvitationAsync(token, ct);
        if (invitation is null || !invitation.IsValid)
        {
            ErrorMessage = "This invitation is invalid or has expired.";
            return Page();
        }
        if (!await EmailMatchesAsync(invitation))
        {
            ErrorMessage = "This invitation was sent to a different email address.";
            return Page();
        }
        BlogTitle = await dbContext.Blogs.AsNoTracking()
            .Where(b => b.Id == invitation.BlogId).Select(b => b.Title).FirstOrDefaultAsync(ct) ?? "this blog";
        Role = invitation.Role;
        return Page();
    }

    public Task<IActionResult> OnPostAcceptAsync(string token, CancellationToken ct = default) =>
        CompleteAsync(token, accept: true, ct);

    public Task<IActionResult> OnPostDeclineAsync(string token, CancellationToken ct = default) =>
        CompleteAsync(token, accept: false, ct);

    private async Task<IActionResult> CompleteAsync(string token, bool accept, CancellationToken ct)
    {
        BlogInvitation? invitation = await FindInvitationAsync(token, ct);
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (invitation is null || !invitation.IsValid || user is null || !await EmailMatchesAsync(invitation))
            return BadRequest("This invitation is invalid, expired, or belongs to another account.");

        if (!accept)
        {
            invitation.Decline();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Declined", user.Id));
            await dbContext.SaveChangesAsync(ct);
            return RedirectToPage("/Dashboard/Index");
        }

        bool isOwner = await dbContext.Blogs.AnyAsync(b => b.Id == invitation.BlogId && b.OwnerId == user.Id, ct);
        bool alreadyMember = await dbContext.BlogMemberships
            .AnyAsync(m => m.BlogId == invitation.BlogId && m.UserId == user.Id, ct);
        if (isOwner || alreadyMember)
        {
            invitation.Cancel();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(
                invitation.Id, isOwner ? "InvalidatedForOwner" : "InvalidatedForExistingMember", user.Id));
        }
        else
        {
            dbContext.BlogMemberships.Add(BlogMembership.Create(
                invitation.BlogId, user.Id, invitation.Role, invitation.InvitedByUserId));
            invitation.Accept();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Accepted", user.Id));
        }
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return StatusCode(StatusCodes.Status409Conflict, "The invitation was changed by another request. Refresh and try again.");
        }

        string? blogSlug = await dbContext.Blogs.AsNoTracking()
            .Where(b => b.Id == invitation.BlogId).Select(b => b.Subdomain).FirstOrDefaultAsync(ct);
        return blogSlug is null ? RedirectToPage("/Dashboard/Index") : Redirect($"/app/admin/{blogSlug}");
    }

    private Task<BlogInvitation?> FindInvitationAsync(string token, CancellationToken ct) =>
        dbContext.BlogInvitations.FirstOrDefaultAsync(i => i.TokenHash == InvitationTokenService.Hash(token), ct);

    private async Task<bool> EmailMatchesAsync(BlogInvitation invitation)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        return user is not null && string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase);
    }
}
