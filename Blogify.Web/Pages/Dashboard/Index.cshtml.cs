using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Pages.Dashboard;

[Authorize]
public sealed class IndexModel(
    IAccessibleBlogService accessibleBlogService,
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<AccessibleBlog> OwnedBlogs { get; private set; } = [];
    public IReadOnlyList<AccessibleBlog> MemberBlogs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        string? userId = userManager.GetUserId(User);
        if (userId is null)
            return RedirectToPage("/Account/Login", new { area = "Identity" });

        IReadOnlyList<AccessibleBlog> blogs = await accessibleBlogService.GetForUserAsync(
            userId, Request.Scheme, Request.Host, ct);
        OwnedBlogs = blogs.Where(blog => blog.IsOwner).ToList();
        MemberBlogs = blogs.Where(blog => !blog.IsOwner).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostLeaveAsync(Guid blogId, CancellationToken ct = default)
    {
        string? userId = userManager.GetUserId(User);
        if (userId is null) return Forbid();

        BlogMembership? membership = await dbContext.BlogMemberships
            .FirstOrDefaultAsync(m => m.BlogId == blogId && m.UserId == userId, ct);
        if (membership is not null)
        {
            dbContext.BlogMemberships.Remove(membership);
            await dbContext.SaveChangesAsync(ct);
        }

        TempData["SuccessMessage"] = "You left the blog.";
        return RedirectToPage();
    }
}
