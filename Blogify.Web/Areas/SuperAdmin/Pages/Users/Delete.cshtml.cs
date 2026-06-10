using System.Security.Claims;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Blogify.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Users;

[Authorize(Roles = "SuperAdmin")]
public sealed class DeleteModel(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    public string UserEmail { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser? user = await userManager.FindByIdAsync(Id);
        if (user is null)
        {
            return NotFound();
        }

        UserEmail = user.Email ?? user.UserName ?? Id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser? user = await userManager.FindByIdAsync(Id);
        if (user is null)
        {
            return NotFound();
        }

        string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == user.Id)
        {
            UserEmail = user.Email ?? user.UserName ?? Id;
            ModelState.AddModelError(string.Empty, localizer["Message.CannotDeleteOwnAccount"]);
            return Page();
        }

        if (await dbContext.Blogs.AnyAsync(b => b.OwnerId == user.Id))
        {
            UserEmail = user.Email ?? user.UserName ?? Id;
            ModelState.AddModelError(string.Empty, "This user owns one or more blogs and cannot be deleted.");
            return Page();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        List<BlogMembership> memberships = await dbContext.BlogMemberships
            .Where(m => m.UserId == user.Id).ToListAsync();
        dbContext.BlogMemberships.RemoveRange(memberships);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            string email = user.Email.Trim().ToLowerInvariant();
            List<BlogInvitation> pendingInvitations = await dbContext.BlogInvitations
                .Where(i => i.Email == email && i.Status == BlogInvitationStatus.Pending)
                .ToListAsync();
            foreach (BlogInvitation invitation in pendingInvitations)
            {
                invitation.Cancel();
                dbContext.BlogInvitationEvents.Add(BlogInvitationEvent.Create(invitation.Id, "CancelledForUserDeletion", currentUserId));
            }
        }
        await dbContext.SaveChangesAsync();

        IdentityResult result = await userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            await transaction.CommitAsync();
            return RedirectToPage("./Index");
        }

        await transaction.RollbackAsync();
        UserEmail = user.Email ?? user.UserName ?? Id;
        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
