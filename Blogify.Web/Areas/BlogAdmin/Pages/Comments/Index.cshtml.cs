using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Comments;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IBlogPermissionService permissionService,
    IPublicBlogCacheInvalidator publicBlogCacheInvalidator,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    private const int PageSize = 100;

    public IReadOnlyList<CommentModerationItemVm> Comments { get; private set; } = [];
    public CommentModerationStatus StatusFilter { get; private set; } = CommentModerationStatus.Pending;

    [BindProperty]
    public ModerationInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(
        CommentModerationStatus status = CommentModerationStatus.Pending,
        CancellationToken ct = default)
    {
        if (!await CanManageCommentsAsync(ct))
        {
            return Forbid();
        }

        StatusFilter = status;
        await LoadCommentsAsync(status, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct = default)
    {
        if (!await CanManageCommentsAsync(ct))
        {
            return Forbid();
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Challenge();
        }

        Comment? comment = await FindCommentAsync(id, ct);
        if (comment is null)
        {
            return NotFound();
        }

        comment.Approve(userId);
        await dbContext.SaveChangesAsync(ct);
        await publicBlogCacheInvalidator.InvalidateTenantAsync(tenantContext.RequiredTenant.Id, ct);

        TempData["SuccessMessage"] = localizer["Comments.Moderation.Approved"].Value;
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct = default)
    {
        if (!await CanManageCommentsAsync(ct))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            await LoadCommentsAsync(CommentModerationStatus.Pending, ct);
            return Page();
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Challenge();
        }

        Comment? comment = await FindCommentAsync(id, ct);
        if (comment is null)
        {
            return NotFound();
        }

        bool wasApproved = comment.IsApproved;
        comment.Reject(userId, Input.Reason);
        await dbContext.SaveChangesAsync(ct);

        if (wasApproved)
        {
            await publicBlogCacheInvalidator.InvalidateTenantAsync(tenantContext.RequiredTenant.Id, ct);
        }

        TempData["SuccessMessage"] = localizer["Comments.Moderation.Rejected"].Value;
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (!await CanManageCommentsAsync(ct))
        {
            return Forbid();
        }

        Comment? comment = await FindCommentAsync(id, ct);
        if (comment is null)
        {
            return NotFound();
        }

        bool wasApproved = comment.IsApproved;
        comment.SoftDelete();
        await dbContext.SaveChangesAsync(ct);

        if (wasApproved)
        {
            await publicBlogCacheInvalidator.InvalidateTenantAsync(tenantContext.RequiredTenant.Id, ct);
        }

        TempData["SuccessMessage"] = localizer["Message.DeleteSuccess"].Value;
        return RedirectToPage(new { blogSlug = RouteData.Values["blogSlug"] });
    }

    private Task<Comment?> FindCommentAsync(Guid id, CancellationToken ct) =>
        dbContext.Comments
            .FirstOrDefaultAsync(c => c.Id == id && c.BlogId == tenantContext.RequiredTenant.Id, ct);

    private async Task LoadCommentsAsync(CommentModerationStatus status, CancellationToken ct)
    {
        List<Comment> comments = await dbContext.Comments
            .AsNoTracking()
            .Where(c => c.BlogId == tenantContext.RequiredTenant.Id && c.ModerationStatus == status)
            .OrderBy(c => c.CreatedAt)
            .Take(PageSize)
            .ToListAsync(ct);

        List<Guid> postIds = comments.Select(c => c.PostId).Distinct().ToList();
        Dictionary<Guid, string> postTitles = await dbContext.Posts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => postIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                Title = dbContext.PostRevisions
                    .Where(r => r.PostId == p.Id)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.Title)
                    .FirstOrDefault() ?? p.Slug
            })
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        List<string> authorIds = comments.Select(c => c.AuthorId).Distinct().ToList();
        Dictionary<string, string> authorNames = await dbContext.Users
            .AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? localizer["Blog.Anonymous"].Value, ct);

        Comments = comments
            .Select(c => new CommentModerationItemVm(
                c.Id,
                postTitles.GetValueOrDefault(c.PostId, localizer["Comments.UnknownPost"].Value),
                authorNames.GetValueOrDefault(c.AuthorId, localizer["Blog.Anonymous"].Value),
                c.Content,
                c.CreatedAt,
                c.ModerationStatus,
                c.ModerationReason))
            .ToList();
    }

    private async Task<bool> CanManageCommentsAsync(CancellationToken ct)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is not null &&
            await permissionService.CanManageCommentsAsync(userId, tenantContext.RequiredTenant.Id, ct);
    }
}

public sealed record CommentModerationItemVm(
    Guid Id,
    string PostTitle,
    string AuthorName,
    string Content,
    DateTimeOffset CreatedAt,
    CommentModerationStatus Status,
    string? ModerationReason);

public sealed class ModerationInput
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}
