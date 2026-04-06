using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class PostModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    public string PostTitle { get; private set; } = string.Empty;
    public string PostContent { get; private set; } = string.Empty;
    public string? CoverImageUrl { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public IReadOnlyList<CategoryLinkViewModel> CategoryItems { get; private set; } = [];
    public string AuthorId { get; private set; } = string.Empty;
    public BlogSidebarViewModel Sidebar { get; private set; } = new([]);

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        Post? post = await dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Revisions)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.PublishedRevisionId != null, ct);

        if (post is null)
        {
            return NotFound();
        }

        if (post.PublishedRevisionId is not Guid publishedRevisionId)
        {
            return NotFound();
        }

        PostRevision? publishedRevision = post.Revisions.FirstOrDefault(r => r.Id == publishedRevisionId);
        if (publishedRevision is null)
        {
            return NotFound();
        }

        PostTitle = publishedRevision.Title;
        PostContent = publishedRevision.Content;
        PublishedAt = publishedRevision.CreatedAt;
        AuthorId = post.AuthorId;

        if (post.CoverImageId is Guid coverImageId)
        {
            Media? media = await dbContext.Media
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == coverImageId, ct);

            CoverImageUrl = media?.Url;
        }

        CategoryItems = await (
            from pc in dbContext.PostCategories.AsNoTracking()
            where pc.PostId == post.Id
            join c in dbContext.Categories.AsNoTracking() on pc.CategoryId equals c.Id
            select new CategoryLinkViewModel(c.Name, c.Slug)
        ).ToListAsync(ct);

        List<CategoryLinkViewModel> sidebarCategories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkViewModel(c.Name, c.Slug))
            .ToListAsync(ct);

        Sidebar = new BlogSidebarViewModel(sidebarCategories);

        return Page();
    }
}
