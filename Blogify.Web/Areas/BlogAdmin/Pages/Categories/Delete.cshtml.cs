using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Categories;

[Authorize]
public sealed class DeleteModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public string CategoryName { get; private set; } = string.Empty;
    public string CategorySlug { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Category? category = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == Id, ct);

        if (category is null)
        {
            return NotFound();
        }

        CategoryName = category.Name;
        CategorySlug = category.Slug;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        Category? category = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == Id, ct);

        if (category is null)
        {
            return NotFound();
        }

        category.SoftDelete();
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("/Categories/Index", new { area = "BlogAdmin" });
    }
}

