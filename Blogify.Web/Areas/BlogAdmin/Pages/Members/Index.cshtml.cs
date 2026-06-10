using System.Globalization;
using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Blogify.Web.Services.Email;
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
    IBlogPermissionService permissionService,
    IAppEmailSender emailSender,
    ILogger<IndexModel> logger) : PageModel
{
    public string OwnerEmail { get; private set; } = string.Empty;
    public IReadOnlyList<MemberListItem> Members { get; private set; } = [];
    public IReadOnlyList<InvitationListItem> PendingInvitations { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Forbid();
        Guid blogId = tenantContext.RequiredTenant.Id;
        if (!await permissionService.CanManageUsersAsync(userId, blogId, ct)) return Forbid();

        ApplicationUser? owner = await userManager.FindByIdAsync(tenantContext.RequiredTenant.OwnerId);
        OwnerEmail = owner?.Email ?? tenantContext.RequiredTenant.OwnerId;
        bool isOwner = await permissionService.IsOwnerAsync(userId, blogId, ct);

        List<BlogMembership> memberships = await dbContext.BlogMemberships.AsNoTracking()
            .Where(m => m.BlogId == blogId).OrderBy(m => m.JoinedAtUtc).ToListAsync(ct);
        List<string> memberUserIds = memberships.Select(m => m.UserId).ToList();
        Dictionary<string, string> emailByUserId = await dbContext.Users.AsNoTracking()
            .Where(u => memberUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.Id, ct);

        Members = memberships.Select(m => new MemberListItem(
            m.Id,
            emailByUserId.TryGetValue(m.UserId, out string? email) ? email : m.UserId,
            m.Role,
            m.JoinedAtUtc.ToString("MMM d, yyyy"),
            isOwner || m.Role is BlogRole.Writer or BlogRole.Editor)).ToList();

        await ExpireInvitationsAsync(userId, blogId, ct);
        PendingInvitations = await dbContext.BlogInvitations.AsNoTracking()
            .Where(i => i.BlogId == blogId && i.Status == BlogInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new InvitationListItem(
                i.Id, i.Email, i.Role, i.ExpiresAtUtc.ToString("MMM d, yyyy"), i.LastSentAtUtc, isOwner || i.Role != BlogRole.Admin))
            .ToListAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid memberId, CancellationToken ct = default)
    {
        (string userId, Guid blogId)? scope = await GetManagerScopeAsync(ct);
        if (scope is null) return Forbid();
        BlogMembership? membership = await dbContext.BlogMemberships
            .FirstOrDefaultAsync(m => m.Id == memberId && m.BlogId == scope.Value.blogId, ct);
        if (membership is null) return NotFound();
        if (!await permissionService.CanManageRoleAsync(scope.Value.userId, scope.Value.blogId, membership.Role, ct)) return Forbid();

        dbContext.BlogMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(ct);
        TempData["SuccessMessage"] = "Member removed.";
        return RedirectToIndex();
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(Guid memberId, string role, CancellationToken ct = default)
    {
        (string userId, Guid blogId)? scope = await GetManagerScopeAsync(ct);
        if (scope is null) return Forbid();
        if (!Enum.TryParse(role, true, out BlogRole newRole)) return BadRequest();
        BlogMembership? membership = await dbContext.BlogMemberships
            .FirstOrDefaultAsync(m => m.Id == memberId && m.BlogId == scope.Value.blogId, ct);
        if (membership is null) return NotFound();
        if (!await permissionService.CanManageRoleAsync(scope.Value.userId, scope.Value.blogId, membership.Role, ct)
            || !await permissionService.CanManageRoleAsync(scope.Value.userId, scope.Value.blogId, newRole, ct))
            return Forbid();

        membership.ChangeRole(newRole);
        await dbContext.SaveChangesAsync(ct);
        TempData["SuccessMessage"] = "Member role updated.";
        return RedirectToIndex();
    }

    public async Task<IActionResult> OnPostCancelInviteAsync(Guid invitationId, CancellationToken ct = default)
    {
        (string userId, Guid blogId)? scope = await GetManagerScopeAsync(ct);
        if (scope is null) return Forbid();
        BlogInvitation? invitation = await FindPendingInvitationAsync(invitationId, scope.Value.blogId, ct);
        if (invitation is null) return NotFound();
        if (!await permissionService.CanManageRoleAsync(scope.Value.userId, scope.Value.blogId, invitation.Role, ct)) return Forbid();

        invitation.Cancel();
        dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Cancelled", scope.Value.userId));
        await dbContext.SaveChangesAsync(ct);
        TempData["SuccessMessage"] = "Invitation cancelled.";
        return RedirectToIndex();
    }

    public async Task<IActionResult> OnPostResendInviteAsync(Guid invitationId, CancellationToken ct = default)
    {
        (string userId, Guid blogId)? scope = await GetManagerScopeAsync(ct);
        if (scope is null) return Forbid();
        BlogInvitation? invitation = await FindPendingInvitationAsync(invitationId, scope.Value.blogId, ct);
        if (invitation is null) return NotFound();
        if (!await permissionService.CanManageRoleAsync(scope.Value.userId, scope.Value.blogId, invitation.Role, ct)) return Forbid();
        if (invitation.IsExpired)
        {
            invitation.Expire();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Expired", scope.Value.userId));
            await dbContext.SaveChangesAsync(ct);
            TempData["WarningMessage"] = "The invitation expired. Create a new invitation.";
            return RedirectToIndex();
        }
        if (invitation.LastSentAtUtc is DateTimeOffset sentAt && sentAt > DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            TempData["WarningMessage"] = "Please wait five minutes before resending.";
            return RedirectToIndex();
        }

        string token = InvitationTokenService.CreateToken();
        invitation.Resend(InvitationTokenService.Hash(token));
        try
        {
            await emailSender.SendBlogInvitationAsync(
                invitation.Email, tenantContext.RequiredTenant.Title, invitation.Role, token, CultureInfo.CurrentUICulture, ct);
            invitation.MarkSent();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Resent", scope.Value.userId));
            TempData["SuccessMessage"] = "Invitation queued.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Invitation {InvitationId} could not be re-enqueued.", invitation.Id);
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "QueueFailed", scope.Value.userId, ex.Message));
            TempData["WarningMessage"] = "The invitation email could not be queued. You can retry immediately.";
        }
        await dbContext.SaveChangesAsync(ct);
        return RedirectToIndex();
    }

    private async Task<(string userId, Guid blogId)?> GetManagerScopeAsync(CancellationToken ct)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return null;
        Guid blogId = tenantContext.RequiredTenant.Id;
        return await permissionService.CanManageUsersAsync(userId, blogId, ct) ? (userId, blogId) : null;
    }

    private Task<BlogInvitation?> FindPendingInvitationAsync(Guid invitationId, Guid blogId, CancellationToken ct) =>
        dbContext.BlogInvitations.FirstOrDefaultAsync(
            i => i.Id == invitationId && i.BlogId == blogId && i.Status == BlogInvitationStatus.Pending, ct);

    private async Task ExpireInvitationsAsync(string actorId, Guid blogId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        List<BlogInvitation> expired = await dbContext.BlogInvitations
            .Where(i => i.BlogId == blogId && i.Status == BlogInvitationStatus.Pending && i.ExpiresAtUtc < now)
            .ToListAsync(ct);
        foreach (BlogInvitation invitation in expired)
        {
            invitation.Expire();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Expired", actorId));
        }
        if (expired.Count > 0) await dbContext.SaveChangesAsync(ct);
    }

    private RedirectToPageResult RedirectToIndex() =>
        RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });

    public sealed record MemberListItem(Guid MembershipId, string Email, BlogRole Role, string JoinedAt, bool CanManage);
    public sealed record InvitationListItem(
        Guid InvitationId, string Email, BlogRole Role, string ExpiresAt, DateTimeOffset? LastSentAtUtc, bool CanManage);
}
