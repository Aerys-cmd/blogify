using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
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
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public InviteInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Forbid();

        bool canManage = await permissionService.CanManageUsersAsync(userId, tenantContext.RequiredTenant.Id, ct);
        if (!canManage) return Forbid();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        string? inviterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (inviterId is null) return Forbid();

        Guid blogId = tenantContext.RequiredTenant.Id;
        bool canManage = await permissionService.CanManageUsersAsync(inviterId, blogId, ct);
        if (!canManage) return Forbid();

        if (!ModelState.IsValid) return Page();

        if (!Enum.TryParse<BlogRole>(Input.Role, ignoreCase: true, out BlogRole role))
        {
            ModelState.AddModelError(nameof(Input.Role), localizer["Message.InvalidRole"]);
            return Page();
        }

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

        // Cancel any existing pending invitations for same email+blog.
        List<BlogInvitation> existing = await dbContext.BlogInvitations
            .Where(i => i.BlogId == blogId
                && i.Email == normalizedEmail
                && i.AcceptedAtUtc == null
                && i.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        dbContext.BlogInvitations.RemoveRange(existing);

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        BlogInvitation invitation = BlogInvitation.Create(blogId, normalizedEmail, role, token, inviterId);
        dbContext.BlogInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(ct);

        // TODO: Send invitation email via IEmailSender once an email provider is configured.
        // Invitation acceptance URL: {Request.Scheme}://{Request.Host}/invite/{token}

        TempData["InviteSent"] = string.Format(
            localizer["BlogAdmin.Members.InviteSentMessage"].Value,
            normalizedEmail);

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
