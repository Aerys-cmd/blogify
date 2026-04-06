using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.SuperAdmin.Pages.Users;

[Authorize(Roles = "SuperAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager) : PageModel
{
    private const int PageSize = 20;

    public IReadOnlyList<UserListItem> Users { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int CurrentPage { get; private set; }
    public int TotalPages { get; private set; }
    public string SearchQuery { get; private set; } = string.Empty;
    public string PaginationBaseUrl { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? q, int page = 1, CancellationToken ct = default)
    {
        SearchQuery = q?.Trim() ?? string.Empty;

        IQueryable<ApplicationUser> query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            string search = SearchQuery;
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(search)) ||
                (u.UserName != null && u.UserName.Contains(search)));
        }

        TotalCount = await query.CountAsync(ct);
        CurrentPage = page < 1 ? 1 : page;
        TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);

        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }

        List<ApplicationUser> pageUsers = await query
            .OrderBy(u => u.Email)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        List<Guid> tenantIds = pageUsers
            .Where(u => u.TenantId.HasValue)
            .Select(u => u.TenantId.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> blogTitles = await dbContext.Blogs
            .AsNoTracking()
            .Where(t => tenantIds.Contains(t.Id) && t.DeletedAt == null)
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);

        List<UserListItem> items = [];
        foreach (ApplicationUser user in pageUsers)
        {
            IList<string> roles = await userManager.GetRolesAsync(user);
            string? blogTitle = user.TenantId.HasValue && blogTitles.TryGetValue(user.TenantId.Value, out string? title)
                ? title
                : null;
            items.Add(new UserListItem(
                user.Id,
                user.Email ?? string.Empty,
                user.UserName ?? string.Empty,
                roles.ToList().AsReadOnly(),
                blogTitle));
        }

        Users = items;

        PaginationBaseUrl = string.IsNullOrEmpty(SearchQuery)
            ? "/sa/Users"
            : $"/sa/Users?q={Uri.EscapeDataString(SearchQuery)}";

        return Page();
    }
}

public sealed record UserListItem(
    string Id,
    string Email,
    string UserName,
    IReadOnlyList<string> Roles,
    string? BlogTitle);
