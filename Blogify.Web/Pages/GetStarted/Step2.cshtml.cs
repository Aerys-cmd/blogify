using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Blogify.Web.Pages.GetStarted;

[Authorize(Roles = "BlogAdmin")]
public sealed class Step2Model(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public required Step2Input Input { get; set; } = new()
    {
        Title = string.Empty,
        Subdomain = string.Empty
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        string? userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/GetStarted/Step1");
        }

        bool alreadyOwns = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.OwnerId == userId && b.DeletedAt == null, ct);

        if (alreadyOwns)
        {
            return RedirectToPage("/GetStarted/Complete");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        string normalizedSubdomain = Input.Subdomain.Trim().ToLowerInvariant();

        bool subdomainTaken = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.Subdomain == normalizedSubdomain && b.DeletedAt == null, ct);

        if (subdomainTaken)
        {
            ModelState.AddModelError(nameof(Input.Subdomain), "This subdomain is already taken. Please choose another.");
            return Page();
        }

        string? userId = userManager.GetUserId(User);
        if (userId is null)
        {
            return RedirectToPage("/GetStarted/Step1");
        }

        try
        {
            Tenant tenant = Tenant.Create(Input.Title, normalizedSubdomain, userId);
            dbContext.Blogs.Add(tenant);
            await dbContext.SaveChangesAsync(ct);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }

        return RedirectToPage("/GetStarted/Complete");
    }

    public sealed record Step2Input
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public required string Title { get; init; }

        [Required]
        [StringLength(63, MinimumLength = 2)]
        [RegularExpression(@"^[A-Za-z0-9-]+$", ErrorMessage = "Subdomain may only contain letters, digits, and hyphens.")]
        public required string Subdomain { get; init; }
    }
}
