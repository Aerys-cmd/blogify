using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
    public Guid PostId { get; private set; }
    public string PostTitle { get; private set; } = string.Empty;
    public string PostContent { get; private set; } = string.Empty;
    public string? CoverImageUrl { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public IReadOnlyList<CategoryLinkViewModel> CategoryItems { get; private set; } = [];
    public string AuthorId { get; private set; } = string.Empty;
    public BlogSidebarViewModel Sidebar { get; private set; } = new([]);
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

        string? authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (authorId is null)
        {
            return Challenge();
        }

        Guid blogId = tenantContext.RequiredTenant.Id;
        if (Input.ParentCommentId is Guid parentCommentId)
        {
            Comment? parentComment = await dbContext.Comments
                .FirstOrDefaultAsync(c => c.Id == parentCommentId && c.BlogId == blogId && c.PostId == PostId, ct);

            if (parentComment is null)
            {
                ModelState.AddModelError(nameof(Input.ParentCommentId), "The selected parent comment is invalid.");
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

        List<Comment> allComments = await dbContext.Comments
            .AsNoTracking()
            .Where(c => c.PostId == post.Id)
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
            ?? "/Identity/Account/Login";

        return null;
    }

    private static CommentViewModel BuildCommentViewModel(
        Comment comment,
        Dictionary<Guid, List<Comment>> repliesByParent,
        Dictionary<string, string> authorNames)
    {
        string authorName = authorNames.GetValueOrDefault(comment.AuthorId, "Anonymous");

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
    [Required(ErrorMessage = "Comment content is required.")]
    [MaxLength(2000, ErrorMessage = "Comment must not exceed 2000 characters.")]
    public string Content { get; init; } = string.Empty;

    public Guid? ParentCommentId { get; init; }
}
