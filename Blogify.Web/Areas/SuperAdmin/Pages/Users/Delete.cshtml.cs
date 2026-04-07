using System.Security.Claims;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Users;

[Authorize(Roles = "SuperAdmin")]
public sealed class DeleteModel(UserManager<ApplicationUser> userManager, IStringLocalizer<SharedResource> localizer) : PageModel
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

        IdentityResult result = await userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            return RedirectToPage("./Index");
        }

        UserEmail = user.Email ?? user.UserName ?? Id;
        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
