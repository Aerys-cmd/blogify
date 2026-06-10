using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Pages.Dashboard;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<BlogCardItem> OwnedBlogs { get; private set; } = [];
    public IReadOnlyList<MemberBlogCardItem> MemberBlogs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        string? userId = userManager.GetUserId(User);
        if (userId is null)
            return RedirectToPage("/Identity/Account/Login", new { area = "Identity" });

        string scheme = Request.Scheme;
        string host = Request.Host.Host;
        string portSuffix = Request.Host.Port.HasValue ? $":{Request.Host.Port}" : string.Empty;

        List<Tenant> owned = await dbContext.Blogs
            .AsNoTracking()
            .Where(b => b.OwnerId == userId && b.DeletedAt == null)
            .OrderBy(b => b.Title)
            .ToListAsync(ct);

        OwnedBlogs = owned
            .Select(b => new BlogCardItem(
                b.Id,
                b.Title,
                b.Subdomain,
                $"{scheme}://{b.Subdomain}.{host}{portSuffix}"))
            .ToList();

        List<MemberBlogEntry> memberEntries = await (
            from m in dbContext.BlogMemberships.AsNoTracking()
            join b in dbContext.Blogs.AsNoTracking() on m.BlogId equals b.Id
            where m.UserId == userId && b.DeletedAt == null
            orderby b.Title
            select new MemberBlogEntry(b.Id, b.Title, b.Subdomain, m.Role)
        ).ToListAsync(ct);

        MemberBlogs = memberEntries
            .Select(e => new MemberBlogCardItem(
                e.Id,
                e.Title,
                e.Subdomain,
                e.Role,
                $"{scheme}://{e.Subdomain}.{host}{portSuffix}"))
            .ToList();

        return Page();
    }

    public sealed record BlogCardItem(Guid Id, string Title, string Subdomain, string BlogUrl);
    public sealed record MemberBlogCardItem(Guid Id, string Title, string Subdomain, BlogRole Role, string BlogUrl);
    private sealed record MemberBlogEntry(Guid Id, string Title, string Subdomain, BlogRole Role);
}
