using Blogify.Web.Data;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Posts;

[Authorize]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    private const int PageSize = 10;

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public IReadOnlyList<PostRow> Posts { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int PublishedCount { get; private set; }
    public int DraftCount { get; private set; }
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public string TenantTitle { get; private set; } = string.Empty;
    public bool HasActiveFilters => !string.IsNullOrEmpty(StatusFilter) || !string.IsNullOrEmpty(SearchQuery);

    public async Task<IActionResult> OnGetAsync(int page = 1, CancellationToken ct = default)
    {
        TenantTitle = tenantContext.RequiredTenant.Title;

        // Unfiltered counts for filter tab badges
        PublishedCount = await dbContext.Posts.CountAsync(p => p.Status == PostStatus.Published, ct);
        DraftCount = await dbContext.Posts.CountAsync(p => p.Status == PostStatus.Draft, ct);

        IQueryable<Post> query = dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Revisions)
            .OrderByDescending(p => p.CreatedAt);

        if (StatusFilter == "published")
        {
            query = query.Where(p => p.Status == PostStatus.Published);
        }
        else if (StatusFilter == "draft")
        {
            query = query.Where(p => p.Status == PostStatus.Draft);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string searchPattern = $"%{SearchQuery.Trim()}%";
            query = query.Where(p => p.Revisions.Any(r => EF.Functions.Like(r.Title, searchPattern)));
        }

        TotalCount = await query.CountAsync(ct);
        CurrentPage = page < 1 ? 1 : page;
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);

        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }

        List<Post> posts = await query
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        List<string> authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();
        Dictionary<string, string> authorNames = authorIds.Count > 0
            ? await dbContext.Users
                .Where(u => authorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? u.Id, ct)
            : [];

        Posts = posts.Select(p =>
        {
            PostRevision? latestRevision = p.Revisions
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            return new PostRow(
                Id: p.Id,
                Title: latestRevision?.Title ?? "(untitled)",
                Slug: p.Slug,
                Status: p.Status,
                AuthorName: authorNames.TryGetValue(p.AuthorId, out string? name) ? name : p.AuthorId,
                CreatedAt: p.CreatedAt,
                Excerpt: p.Excerpt
            );
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Post? post = await dbContext.Posts
            .Include(p => p.Revisions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (post is null)
        {
            return NotFound();
        }

        post.SoftDelete();
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }
}

public sealed record PostRow(
    Guid Id,
    string Title,
    string Slug,
    PostStatus Status,
    string AuthorName,
    DateTimeOffset CreatedAt,
    string? Excerpt
);
