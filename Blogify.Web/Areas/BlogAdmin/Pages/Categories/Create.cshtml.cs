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

namespace Blogify.Web.Areas.BlogAdmin.Pages.Categories;

[Authorize]
public sealed class CreateModel(ApplicationDbContext dbContext, TenantContext tenantContext, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public CategoryInputModel Input { get; set; } = new();

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

        bool slugTaken = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug == slug, ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), localizer["Message.CategorySlugTaken"]);
            return Page();
        }

        bool nameTaken = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Name == name, ct);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(Input.Name), localizer["Message.CategoryNameTaken"]);
            return Page();
        }

        Category category = Category.Create(blogId, name, slug);
        category.UpdateSeoMetadata(Input.MetaTitle, Input.MetaDescription);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("/Categories/Index", new { area = "BlogAdmin" });
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

public sealed class CategoryInputModel
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    [RegularExpression(@"^[a-z0-9-]*$")]
    public string? Slug { get; set; }

    [MaxLength(60)]
    public string? MetaTitle { get; set; }

    [MaxLength(160)]
    public string? MetaDescription { get; set; }
}

