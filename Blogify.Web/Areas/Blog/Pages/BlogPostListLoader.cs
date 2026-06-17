using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.Blog.Pages;

internal static class BlogPostListLoader
{
    public const int PageSize = 10;

    public static async Task<BlogSidebarViewModel> LoadSidebarAsync(ApplicationDbContext dbContext, CancellationToken ct)
    {
        List<CategoryLinkViewModel> categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryLinkViewModel(c.Name, c.Slug))
            .ToListAsync(ct);

        List<TagLinkViewModel> tags = await dbContext.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagLinkViewModel(t.Name, t.Slug))
            .ToListAsync(ct);

        return new BlogSidebarViewModel(categories, tags);
    }

    public static async Task<PagedPostListViewModel> LoadPostsAsync(
        ApplicationDbContext dbContext,
        IQueryable<Post> query,
        int page,
        CancellationToken ct)
    {
        int totalCount = await query.CountAsync(ct);
        int totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
        int currentPage = page < 1 ? 1 : page;
        if (currentPage > totalPages && totalPages > 0)
        {
            currentPage = totalPages;
        }

        List<PostQueryRow> rows = await (
            from p in query
            where p.PublishedRevisionId != null
            join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
            orderby r.CreatedAt descending
            select new PostQueryRow(p.Id, p.Slug, p.Excerpt, p.CoverImageId, r.Title, r.CreatedAt)
        )
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return new PagedPostListViewModel([], new PaginationViewModel(currentPage, totalPages, totalCount));
        }

        List<Guid> coverImageIds = rows
            .Where(p => p.CoverImageId.HasValue)
            .Select(p => p.CoverImageId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> coverUrls = coverImageIds.Count > 0
            ? await dbContext.Media
                .AsNoTracking()
                .Where(m => coverImageIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, m => m.ThumbnailUrl ?? m.Url, ct)
            : [];

        List<Guid> postIds = rows.Select(p => p.Id).ToList();

        List<PostCategoryRow> categoryRows = await (
            from pc in dbContext.PostCategories.AsNoTracking()
            where postIds.Contains(pc.PostId)
            join c in dbContext.Categories.AsNoTracking() on pc.CategoryId equals c.Id
            select new PostCategoryRow(pc.PostId, c.Name, c.Slug)
        ).ToListAsync(ct);

        List<PostTagRow> tagRows = await (
            from pt in dbContext.PostTags.AsNoTracking()
            where postIds.Contains(pt.PostId)
            join t in dbContext.Tags.AsNoTracking() on pt.TagId equals t.Id
            select new PostTagRow(pt.PostId, t.Name, t.Slug)
        ).ToListAsync(ct);

        Dictionary<Guid, List<CategoryLinkViewModel>> categoryMap = categoryRows
            .GroupBy(x => x.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new CategoryLinkViewModel(x.Name, x.Slug)).ToList());

        Dictionary<Guid, List<TagLinkViewModel>> tagMap = tagRows
            .GroupBy(x => x.PostId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new TagLinkViewModel(x.Name, x.Slug)).ToList());

        List<PostCardViewModel> posts = rows.Select(p => new PostCardViewModel(
            p.Slug,
            p.Title,
            p.Excerpt,
            p.CoverImageId.HasValue && coverUrls.TryGetValue(p.CoverImageId.Value, out string? url) ? url : null,
            p.PublishedAt,
            categoryMap.TryGetValue(p.Id, out List<CategoryLinkViewModel>? cats) ? cats : [],
            tagMap.TryGetValue(p.Id, out List<TagLinkViewModel>? tags) ? tags : []
        )).ToList();

        return new PagedPostListViewModel(posts, new PaginationViewModel(currentPage, totalPages, totalCount));
    }

    private sealed record PostQueryRow(
        Guid Id,
        string Slug,
        string? Excerpt,
        Guid? CoverImageId,
        string Title,
        DateTimeOffset PublishedAt);

    private sealed record PostCategoryRow(Guid PostId, string Name, string Slug);
    private sealed record PostTagRow(Guid PostId, string Name, string Slug);
}

public sealed record PagedPostListViewModel(
    IReadOnlyList<PostCardViewModel> Posts,
    PaginationViewModel Pagination);

public sealed record PaginationViewModel(int CurrentPage, int TotalPages, int TotalCount)
{
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}
