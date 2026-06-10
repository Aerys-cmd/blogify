using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Members;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    UserManager<ApplicationUser> userManager,
    IBlogPermissionService permissionService) : PageModel
{
    public bool CanManageUsers { get; private set; }
    public string OwnerEmail { get; private set; } = string.Empty;
    public IReadOnlyList<MemberListItem> Members { get; private set; } = [];
    public IReadOnlyList<InvitationListItem> PendingInvitations { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Forbid();

        Guid blogId = tenantContext.RequiredTenant.Id;
        CanManageUsers = await permissionService.CanManageUsersAsync(userId, blogId, ct);

        ApplicationUser? owner = await userManager.FindByIdAsync(tenantContext.RequiredTenant.OwnerId);
        OwnerEmail = owner?.Email ?? tenantContext.RequiredTenant.OwnerId;

        List<BlogMembership> memberships = await dbContext.BlogMemberships
            .AsNoTracking()
            .Where(m => m.BlogId == blogId)
            .OrderBy(m => m.JoinedAtUtc)
            .ToListAsync(ct);

        List<string> memberUserIds = memberships.Select(m => m.UserId).ToList();
        Dictionary<string, string> emailByUserId = await dbContext.Users
            .AsNoTracking()
            .Where(u => memberUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id, ct);

        Members = memberships.Select(m => new MemberListItem(
            m.Id,
            emailByUserId.TryGetValue(m.UserId, out string? e) ? e : m.UserId,
            m.Role,
            m.JoinedAtUtc.ToString("MMM d, yyyy"))).ToList();

        var now = DateTimeOffset.UtcNow;

        PendingInvitations = await dbContext.BlogInvitations
            .AsNoTracking()
            .Where(i => i.BlogId == blogId && i.AcceptedAtUtc == null && i.ExpiresAtUtc > now)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvitationListItem(i.Email, i.Role, i.ExpiresAtUtc.ToString("MMM d, yyyy")))
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid memberId, CancellationToken ct = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Forbid();

        Guid blogId = tenantContext.RequiredTenant.Id;
        if (!await permissionService.CanManageUsersAsync(userId, blogId, ct))
            return Forbid();

        BlogMembership? membership = await dbContext.BlogMemberships
            .FirstOrDefaultAsync(m => m.Id == memberId && m.BlogId == blogId, ct);

        if (membership is null) return NotFound();

        dbContext.BlogMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    public sealed record MemberListItem(Guid MembershipId, string Email, BlogRole Role, string JoinedAt);
    public sealed record InvitationListItem(string Email, BlogRole Role, string ExpiresAt);
}
