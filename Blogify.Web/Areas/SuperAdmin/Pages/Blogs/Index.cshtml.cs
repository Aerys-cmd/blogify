using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Blogs;

[Authorize(Roles = "SuperAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    private const int PageSize = 20;

    public IReadOnlyList<BlogListItem> Blogs { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public string SearchQuery { get; private set; } = string.Empty;
    public string PaginationBaseUrl { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? q, int page = 1, CancellationToken ct = default)
    {
        SearchQuery = q?.Trim() ?? string.Empty;

        IQueryable<Tenant> query = dbContext.Blogs
            .AsNoTracking();

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            string search = SearchQuery;
            query = query.Where(t =>
                t.Title.Contains(search) ||
                t.Subdomain.Contains(search));
        }

        TotalCount = await query.CountAsync(ct);
        CurrentPage = page < 1 ? 1 : page;
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);

        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }

        List<Tenant> tenants = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        List<string> ownerIds = tenants.Select(t => t.OwnerId).Distinct().ToList();
        Dictionary<string, string> ownerEmails = await dbContext.Users
            .AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id, ct);

        Blogs = tenants.Select(t => new BlogListItem(
            t.Id,
            t.Title,
            t.Subdomain,
            ownerEmails.TryGetValue(t.OwnerId, out string? email) ? email : t.OwnerId,
            t.ActiveTheme,
            t.CreatedAt)).ToList();

        PaginationBaseUrl = string.IsNullOrEmpty(SearchQuery)
            ? "/sa/Blogs"
            : $"/sa/Blogs?q={Uri.EscapeDataString(SearchQuery)}";

        return Page();
    }
}

public sealed record BlogListItem(
    Guid Id,
    string Title,
    string Subdomain,
    string OwnerEmail,
    string ActiveTheme,
    DateTimeOffset CreatedAt);
