using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Blogs;

[Authorize(Roles = "SuperAdmin")]
public sealed class DeleteModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public string TenantTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Tenant? tenant = await dbContext.Blogs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == Id, ct);

        if (tenant is null)
        {
            return NotFound();
        }

        TenantTitle = tenant.Title;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        Tenant? tenant = await dbContext.Blogs
            .FirstOrDefaultAsync(t => t.Id == Id, ct);

        if (tenant is null)
        {
            return NotFound();
        }

        tenant.SoftDelete();
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("./Index");
    }
}
