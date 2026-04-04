using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Categories;

[Authorize(Roles = "BlogAdmin")]
public sealed class EditModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public CategoryEditInput Input { get; set; } = new();

    public string CategoryName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Category? category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == Id, ct);

        if (category is null)
        {
            return NotFound();
        }

        CategoryName = category.Name;
        Input = new CategoryEditInput
        {
            Name = category.Name,
            Slug = category.Slug
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        Category? category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == Id, ct);

        if (category is null)
        {
            return NotFound();
        }

        string name = Input.Name.Trim();
        string slug = string.IsNullOrWhiteSpace(Input.Slug)
            ? GenerateSlug(name)
            : Input.Slug.Trim().ToLowerInvariant();

        bool slugTaken = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Slug == slug && c.Id != Id, ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), "A category with this slug already exists for this blog.");
            CategoryName = category.Name;
            return Page();
        }

        bool nameTaken = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Name == name && c.Id != Id, ct);

        if (nameTaken)
        {
            ModelState.AddModelError(nameof(Input.Name), "A category with this name already exists for this blog.");
            CategoryName = category.Name;
            return Page();
        }

        category.Update(name, slug);
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

public sealed class CategoryEditInput
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name must not exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100, ErrorMessage = "Slug must not exceed 100 characters.")]
    [RegularExpression(@"^[a-z0-9-]*$", ErrorMessage = "Slug may only contain lowercase letters, digits, and hyphens.")]
    public string? Slug { get; set; }
}


