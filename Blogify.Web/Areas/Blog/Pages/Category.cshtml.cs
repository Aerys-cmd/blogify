using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class CategoryModel(ApplicationDbContext dbContext) : PageModel
{
    public string CategoryName { get; private set; } = string.Empty;
    public string CategorySlug { get; private set; } = string.Empty;
    public IReadOnlyList<PostCardViewModel> Posts { get; private set; } = [];
    public BlogSidebarViewModel Sidebar { get; private set; } = new([]);

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        Category? category = await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug, ct);

        if (category is null)
        {
            return NotFound();
        }

        CategoryName = category.Name;
        CategorySlug = category.Slug;

        ViewData["Title"] = category.Name;
        ViewData["MetaTitle"] = category.MetaTitle ?? category.Name;
        ViewData["MetaDescription"] = category.MetaDescription;
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";

        List<CategoryLinkViewModel> sidebarCategories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkViewModel(c.Name, c.Slug))
            .ToListAsync(ct);

        Sidebar = new BlogSidebarViewModel(sidebarCategories);

        List<PostQueryRow> rows = await (
            from pc in dbContext.PostCategories.AsNoTracking()
            where pc.CategoryId == category.Id
            join p in dbContext.Posts.AsNoTracking() on pc.PostId equals p.Id
            where p.PublishedRevisionId != null
            join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
            orderby r.CreatedAt descending
            select new PostQueryRow(p.Id, p.Slug, p.Excerpt, p.CoverImageId, r.Title, r.CreatedAt)
        ).ToListAsync(ct);

        if (rows.Count == 0)
        {
            Posts = [];
            return Page();
        }

        List<Guid> coverImageIds = rows
            .Where(p => p.CoverImageId.HasValue)
            .Select(p => p.CoverImageId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> coverUrls = [];
        if (coverImageIds.Count > 0)
        {
            coverUrls = await dbContext.Media
                .AsNoTracking()
                .Where(m => coverImageIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.Url, ct);
        }

        List<Guid> postIds = rows.Select(p => p.Id).ToList();

        List<PostCategoryRow> categoryRows = await (
            from pc in dbContext.PostCategories.AsNoTracking()
            where postIds.Contains(pc.PostId)
            join c in dbContext.Categories.AsNoTracking() on pc.CategoryId equals c.Id
            select new PostCategoryRow(pc.PostId, c.Name, c.Slug)
        ).ToListAsync(ct);

        Dictionary<Guid, List<CategoryLinkViewModel>> categoryMap = categoryRows
            .GroupBy(x => x.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new CategoryLinkViewModel(x.Name, x.Slug)).ToList());

        Posts = rows.Select(p => new PostCardViewModel(
            p.Slug,
            p.Title,
            p.Excerpt,
            p.CoverImageId.HasValue && coverUrls.TryGetValue(p.CoverImageId.Value, out string? url) ? url : null,
            p.PublishedAt,
            categoryMap.TryGetValue(p.Id, out List<CategoryLinkViewModel>? cats) ? cats : []
        )).ToList();

        return Page();
    }

    private sealed record PostQueryRow(
        Guid Id,
        string Slug,
        string? Excerpt,
        Guid? CoverImageId,
        string Title,
        DateTimeOffset PublishedAt);

    private sealed record PostCategoryRow(Guid PostId, string Name, string Slug);
}
