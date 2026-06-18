using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.Blog.Pages;

public sealed class PostModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IBlockNoteHtmlRenderer htmlRenderer,
    ILogger<PostModel> logger,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    private const int CommentRateLimitCount = 3;
    private static readonly TimeSpan CommentRateLimitWindow = TimeSpan.FromMinutes(2);

    public Guid PostId { get; private set; }
    public string PostTitle { get; private set; } = string.Empty;
    public string PostContent { get; private set; } = string.Empty;
    public string? CoverImageUrl { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public IReadOnlyList<CategoryLinkViewModel> CategoryItems { get; private set; } = [];
    public IReadOnlyList<TagLinkViewModel> TagItems { get; private set; } = [];
    public string AuthorId { get; private set; } = string.Empty;
    public BlogSidebarViewModel Sidebar { get; private set; } = new([], []);
    public IReadOnlyList<CommentViewModel> Comments { get; private set; } = [];
    public bool IsAuthenticated { get; private set; }
    public string LoginUrl { get; private set; } = string.Empty;

    [BindProperty]
    public CommentInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        IActionResult? loadResult = await LoadPostDataAsync(slug, ct);
        return loadResult ?? Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken ct)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return Challenge();
        }

        IActionResult? loadResult = await LoadPostDataAsync(slug, ct);
        if (loadResult is not null)
        {
            return loadResult;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(Input.Website))
        {
            logger.LogWarning(
                "Rejected honeypot comment attempt for blog {BlogId} and post {PostId}.",
                tenantContext.RequiredTenant.Id,
                PostId);
            return RedirectToPage("./Post", new { slug });
        }

        string? authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (authorId is null)
        {
            return Challenge();
        }

        Guid blogId = tenantContext.RequiredTenant.Id;
        DateTimeOffset windowStart = DateTimeOffset.UtcNow.Subtract(CommentRateLimitWindow);
        int recentCommentCount = await dbContext.Comments
            .IgnoreQueryFilters()
            .CountAsync(c =>
                c.BlogId == blogId &&
                c.AuthorId == authorId &&
                c.CreatedAt >= windowStart,
                ct);

        if (recentCommentCount >= CommentRateLimitCount)
        {
            ModelState.AddModelError(string.Empty, localizer["Blog.CommentRateLimited"]);
            return Page();
        }

        if (Input.ParentCommentId is Guid parentCommentId)
        {
            Comment? parentComment = await dbContext.Comments
                .FirstOrDefaultAsync(c =>
                    c.Id == parentCommentId &&
                    c.BlogId == blogId &&
                    c.PostId == PostId &&
                    c.ModerationStatus == CommentModerationStatus.Approved,
                    ct);

            if (parentComment is null)
            {
                ModelState.AddModelError(nameof(Input.ParentCommentId), localizer["Blog.InvalidParentComment"]);
                return Page();
            }
        }

        Comment comment = Comment.Create(blogId, PostId, authorId, Input.Content, Input.ParentCommentId);
        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("./Post", new { slug });
    }

    private async Task<IActionResult?> LoadPostDataAsync(string slug, CancellationToken ct)
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

        PostId = post.Id;
        PostTitle = publishedRevision.Title;
        PostContent = htmlRenderer.Render(publishedRevision.Content);
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

        TagItems = await (
            from pt in dbContext.PostTags.AsNoTracking()
            where pt.PostId == post.Id
            join t in dbContext.Tags.AsNoTracking() on pt.TagId equals t.Id
            select new TagLinkViewModel(t.Name, t.Slug)
        ).ToListAsync(ct);

        Sidebar = await BlogPostListLoader.LoadSidebarAsync(dbContext, ct);

        List<Comment> allComments = await dbContext.Comments
            .AsNoTracking()
            .Where(c => c.PostId == post.Id && c.ModerationStatus == CommentModerationStatus.Approved)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        List<string> commentAuthorIds = allComments.Select(c => c.AuthorId).Distinct().ToList();
        Dictionary<string, string> authorNames = await dbContext.Users
            .AsNoTracking()
            .Where(u => commentAuthorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "Anonymous", ct);

        Dictionary<Guid, List<Comment>> repliesByParent = allComments
            .Where(c => c.ParentCommentId.HasValue)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        Comments = allComments
            .Where(c => c.ParentCommentId is null)
            .Select(c => BuildCommentViewModel(c, repliesByParent, authorNames))
            .ToList();

        IsAuthenticated = User.Identity?.IsAuthenticated ?? false;
        string returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
        LoginUrl = Url.Page("/Account/Login", pageHandler: null, values: new { area = "Identity", returnUrl })
            ?? "/login";

        ViewData["MetaTitle"] = post.MetaTitle ?? publishedRevision.Title;
        ViewData["Title"] = publishedRevision.Title;
        ViewData["MetaDescription"] = post.MetaDescription ?? post.Excerpt;
        ViewData["OgImage"] = CoverImageUrl;
        ViewData["OgType"] = "article";
        ViewData["CanonicalUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{Request.Path}";

        return null;
    }

    private CommentViewModel BuildCommentViewModel(
        Comment comment,
        Dictionary<Guid, List<Comment>> repliesByParent,
        Dictionary<string, string> authorNames)
    {
        string authorName = authorNames.GetValueOrDefault(comment.AuthorId, localizer["Blog.Anonymous"]);

        List<CommentViewModel> replies = repliesByParent.TryGetValue(comment.Id, out List<Comment>? children)
            ? children.Select(c => BuildCommentViewModel(c, repliesByParent, authorNames)).ToList()
            : [];

        return new CommentViewModel(comment.Id, authorName, comment.Content, comment.CreatedAt, replies);
    }
}

public sealed record CommentViewModel(
    Guid Id,
    string AuthorName,
    string Content,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CommentViewModel> Replies);

public sealed record CommentInput
{
    [Required(ErrorMessage = "Blog.CommentRequired")]
    [MaxLength(2000, ErrorMessage = "Blog.CommentTooLong")]
    public string Content { get; init; } = string.Empty;

    public Guid? ParentCommentId { get; init; }

    [MaxLength(100)]
    public string? Website { get; init; }
}
