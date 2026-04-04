using Blogify.Web.Data;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Posts;

[Authorize(Roles = "BlogAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    private const int PageSize = 10;

    public IReadOnlyList<PostRow> Posts { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public string TenantTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int page = 1, CancellationToken ct = default)
    {
        TenantTitle = tenantContext.RequiredTenant.Title;

        IQueryable<Post> query = dbContext.Posts
            .AsNoTracking()
            .Include(p => p.Revisions)
            .OrderByDescending(p => p.CreatedAt);

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
                AuthorId: p.AuthorId,
                CreatedAt: p.CreatedAt
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

        return RedirectToPage();
    }
}

public sealed record PostRow(
    Guid Id,
    string Title,
    string Slug,
    PostStatus Status,
    string AuthorId,
    DateTimeOffset CreatedAt
);
