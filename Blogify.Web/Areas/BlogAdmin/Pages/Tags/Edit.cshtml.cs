using System.ComponentModel.DataAnnotations;
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
public sealed class EditModel(
    ApplicationDbContext dbContext,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public TagEditInput Input { get; set; } = new();

    public string TagName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Tag? tag = await dbContext.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == Id, ct);

        if (tag is null)
        {
            return NotFound();
        }

        TagName = tag.Name;
        Input = new TagEditInput
        {
            Name = tag.Name,
            Slug = tag.Slug
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Tag? tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == Id, ct);
        if (tag is null)
        {
            return NotFound();
        }

        string name = Input.Name.Trim();
        string slug = Input.Slug.Trim().ToLowerInvariant();

        bool slugTaken = await dbContext.Tags
            .AsNoTracking()
            .AnyAsync(t => t.Slug == slug && t.Id != Id, ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), localizer["Message.TagSlugTaken"]);
            TagName = tag.Name;
            return Page();
        }

        bool nameTaken = await dbContext.Tags
            .AsNoTracking()
            .AnyAsync(t => t.Name == name && t.Id != Id, ct);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(Input.Name), localizer["Message.TagNameTaken"]);
            TagName = tag.Name;
            return Page();
        }

        tag.Rename(name);
        tag.ChangeSlug(slug);
        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(tag.BlogId, ct);

        return RedirectToPage("/Tags/Index", new { area = "BlogAdmin", blogSlug = RouteData.Values["blogSlug"] });
    }
}

public sealed class TagEditInput
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [RegularExpression(@"^[a-z0-9-]+$")]
    public string Slug { get; set; } = string.Empty;
}
