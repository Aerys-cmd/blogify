using System.ComponentModel.DataAnnotations;
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
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Members;

[Authorize]
public sealed class InviteModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    UserManager<ApplicationUser> userManager,
    IBlogPermissionService permissionService,
    IStringLocalizer<SharedResource> localizer,
    IAppEmailSender emailSender,
    ILogger<InviteModel> logger) : PageModel
{
    [BindProperty]
    public InviteInput Input { get; set; } = new();
    public bool CanInviteAdmin { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Forbid();

        bool canManage = await permissionService.CanManageUsersAsync(userId, tenantContext.RequiredTenant.Id, ct);
        if (!canManage) return Forbid();
        CanInviteAdmin = await permissionService.IsOwnerAsync(userId, tenantContext.RequiredTenant.Id, ct);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        string? inviterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inviterId is null) return Forbid();

        Guid blogId = tenantContext.RequiredTenant.Id;
        bool canManage = await permissionService.CanManageUsersAsync(inviterId, blogId, ct);
        if (!canManage) return Forbid();

        CanInviteAdmin = await permissionService.IsOwnerAsync(inviterId, blogId, ct);
        if (!ModelState.IsValid) return Page();

        if (!Enum.TryParse<BlogRole>(Input.Role, ignoreCase: true, out BlogRole role))
        {
            ModelState.AddModelError(nameof(Input.Role), localizer["Message.InvalidRole"]);
            return Page();
        }
        if (!await permissionService.CanManageRoleAsync(inviterId, blogId, role, ct))
            return Forbid();

        string normalizedEmail = Input.Email.Trim().ToLowerInvariant();

        // Verify invitee is not already the owner.
        if (string.Equals(tenantContext.RequiredTenant.OwnerId,
            (await userManager.FindByEmailAsync(normalizedEmail))?.Id,
            StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(Input.Email), localizer["BlogAdmin.Members.Error.AlreadyOwner"]);
            return Page();
        }

        // Verify invitee is not already a member.
        ApplicationUser? existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            bool alreadyMember = await dbContext.BlogMemberships
                .AnyAsync(m => m.BlogId == blogId && m.UserId == existingUser.Id, ct);
            if (alreadyMember)
            {
                ModelState.AddModelError(nameof(Input.Email), localizer["BlogAdmin.Members.Error.AlreadyMember"]);
                return Page();
            }
        }

        BlogInvitation? existing = await dbContext.BlogInvitations
            .FirstOrDefaultAsync(i => i.BlogId == blogId
                && i.Email == normalizedEmail
                && i.Status == BlogInvitationStatus.Pending, ct);
        if (existing is not null)
        {
            if (!existing.IsExpired)
            {
                ModelState.AddModelError(nameof(Input.Email), "A pending invitation already exists. Resend or cancel it from the members page.");
                return Page();
            }

            existing.Expire();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(existing.Id, "Expired", inviterId));
        }

        string token = InvitationTokenService.CreateToken();
        BlogInvitation invitation = BlogInvitation.Create(
            blogId, normalizedEmail, role, InvitationTokenService.Hash(token), inviterId);
        dbContext.BlogInvitations.Add(invitation);
        dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Created", inviterId));
        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Input.Email), "A pending invitation already exists.");
            return Page();
        }

        try
        {
            await emailSender.SendBlogInvitationAsync(
                normalizedEmail,
                tenantContext.RequiredTenant.Title,
                role,
                token,
                CultureInfo.CurrentUICulture,
                ct);
            invitation.MarkSent();
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "Queued", inviterId));
            await dbContext.SaveChangesAsync(ct);
            TempData["SuccessMessage"] = "Invitation queued.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Invitation {InvitationId} was persisted but its email could not be enqueued.",
                invitation.Id);
            dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "QueueFailed", inviterId, ex.Message));
            await dbContext.SaveChangesAsync(ct);
            TempData["WarningMessage"] = "Invitation created, but its email could not be queued. Use Resend to try again.";
        }

        return RedirectToPage("/Members/Index", new { area = "BlogAdmin", blogSlug = RouteData.Values["blogSlug"] });
    }

    public sealed record InviteInput
    {
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; init; } = string.Empty;

        [Required]
        public string Role { get; init; } = "Writer";
    }
}
