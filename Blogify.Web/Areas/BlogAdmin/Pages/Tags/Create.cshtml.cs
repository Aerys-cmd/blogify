using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Tags;

[Authorize]
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public TagInputModel Input { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Guid blogId = tenantContext.RequiredTenant.Id;
        string name = Input.Name.Trim();
        string slug = string.IsNullOrWhiteSpace(Input.Slug)
            ? GenerateSlug(name)
            : Input.Slug.Trim().ToLowerInvariant();

        bool slugTaken = await dbContext.Tags
            .AsNoTracking()
            .AnyAsync(t => t.Slug == slug, ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), localizer["Message.TagSlugTaken"]);
            return Page();
        }

        bool nameTaken = await dbContext.Tags
            .AsNoTracking()
            .AnyAsync(t => t.Name == name, ct);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(Input.Name), localizer["Message.TagNameTaken"]);
            return Page();
        }

        Tag tag = Tag.Create(blogId, name, slug);
        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(blogId, ct);

        return RedirectToPage("/Tags/Index", new { area = "BlogAdmin", blogSlug = RouteData.Values["blogSlug"] });
    }

    private static string GenerateSlug(string name)
    {
        string result = name.ToLowerInvariant().Trim();
        result = Regex.Replace(result, @"[^a-z0-9\s-]", string.Empty);
        result = Regex.Replace(result, @"\s+", "-");
        result = Regex.Replace(result, @"-+", "-");
        return result.Trim('-');
    }
}

public sealed class TagInputModel
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    [RegularExpression(@"^[a-z0-9-]*$")]
    public string? Slug { get; set; }
}
