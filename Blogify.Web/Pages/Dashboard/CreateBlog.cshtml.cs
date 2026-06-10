using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Pages.Dashboard;

[Authorize]
public sealed class CreateBlogModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public CreateBlogInput Input { get; set; } = new() { Title = string.Empty, Subdomain = string.Empty };

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return Page();

        string? userId = userManager.GetUserId(User);
        if (userId is null)
            return RedirectToPage("/Identity/Account/Login", new { area = "Identity" });

        string normalizedSubdomain = Input.Subdomain.Trim().ToLowerInvariant();

        bool subdomainTaken = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.Subdomain == normalizedSubdomain && b.DeletedAt == null, ct);

        if (subdomainTaken)
        {
            ModelState.AddModelError(nameof(Input.Subdomain), localizer["Message.SubdomainTaken"]);
            return Page();
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
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Input.Subdomain), localizer["Message.SubdomainTaken"]);
            return Page();
        }

        TempData["BlogCreated"] = Input.Title;
        return RedirectToPage("/Dashboard/Index");
    }

    public sealed record CreateBlogInput
    {
        [Required]
        [StringLength(200, MinimumLength = 2)]
        public required string Title { get; init; }

        [Required]
        [StringLength(63, MinimumLength = 2)]
        [RegularExpression(@"^[A-Za-z0-9-]+$")]
        public required string Subdomain { get; init; }
    }
}
