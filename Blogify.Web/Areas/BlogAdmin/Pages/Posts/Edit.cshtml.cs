using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Posts;

[Authorize(Roles = "BlogAdmin")]
public sealed class EditModel(ApplicationDbContext dbContext, FeedService feedService, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public EditPostInput Input { get; set; } = new();

    [BindProperty]
    public List<Guid> SelectedCategoryIds { get; set; } = [];

    public IReadOnlyList<CategorySelectItem> AvailableCategories { get; private set; } = [];
    public string PostTitle { get; private set; } = string.Empty;
    public string? CoverImagePreviewUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        var postWithCover = await dbContext.Posts
            .AsNoTracking()
            .Where(p => p.Id == Id)
            .Select(p => new
            {
                Post = p,
                CoverUrl = p.CoverImageId != null
                    ? dbContext.Media
                        .Where(m => m.Id == p.CoverImageId)
                        .Select(m => m.ThumbnailUrl ?? m.Url)
                        .FirstOrDefault()
                    : null
            })
            .FirstOrDefaultAsync(ct);

        if (postWithCover is null || postWithCover.Post.DeletedAt.HasValue)
        {
            return NotFound();
        }

        Post post = postWithCover.Post;
        CoverImagePreviewUrl = postWithCover.CoverUrl;

        IReadOnlyList<PostRevision> revisions = await dbContext.PostRevisions
            .AsNoTracking()
            .Where(r => r.PostId == Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        PostRevision latestRevision = revisions.First();

        PostTitle = latestRevision.Title;
        SelectedCategoryIds = await dbContext.PostCategories
            .AsNoTracking()
            .Where(pc => pc.PostId == Id)
            .Select(pc => pc.CategoryId)
            .ToListAsync(ct);

        Input = new EditPostInput
        {
            Title = latestRevision.Title,
            Slug = post.Slug,
            Excerpt = post.Excerpt,
            Content = latestRevision.Content,
            CoverImageId = post.CoverImageId,
            IsPublished = post.Status == PostStatus.Published
        };

        await LoadAvailableCategoriesAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            await LoadAvailableCategoriesAsync(ct);
            return Page();
        }

        Post? post = await dbContext.Posts
            .Include(p => p.Revisions)
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.Id == Id, ct);

        if (post is null || post.DeletedAt.HasValue)
        {
            return NotFound();
        }

        string normalizedSlug = Input.Slug.Trim().ToLowerInvariant();

        bool slugTaken = await dbContext.Posts
            .AsNoTracking()
            .AnyAsync(p => p.Slug == normalizedSlug && p.Id != Id, ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), localizer["Message.SlugTaken"]);
            await LoadAvailableCategoriesAsync(ct);
            return Page();
        }

        if (post.Slug != normalizedSlug)
        {
            post.ChangeSlug(normalizedSlug);
        }

        PostRevision latestRevision = post.Revisions
            .OrderByDescending(r => r.CreatedAt)
            .First();

        bool contentChanged = latestRevision.Title != Input.Title.Trim()
                              || latestRevision.Content != Input.Content.Trim();

        if (contentChanged)
        {
            post.AddRevision(Input.Title, Input.Content);
        }

        post.UpdateExcerpt(Input.Excerpt);
        post.SetCoverImage(Input.CoverImageId);
        post.SetCategories(SelectedCategoryIds);

        bool currentlyPublished = post.Status == PostStatus.Published;

        if (Input.IsPublished && !currentlyPublished)
        {
            PostRevision revisionToPublish = post.Revisions
                .OrderByDescending(r => r.CreatedAt)
                .First();
            post.Publish(revisionToPublish.Id);
        }
        else if (!Input.IsPublished && currentlyPublished)
        {
            post.Unpublish();
        }
        else if (Input.IsPublished && currentlyPublished && contentChanged)
        {
            PostRevision revisionToPublish = post.Revisions
                .OrderByDescending(r => r.CreatedAt)
                .First();
            post.Publish(revisionToPublish.Id);
        }

        await dbContext.SaveChangesAsync(ct);
        feedService.InvalidateTenant(post.BlogId);
        return RedirectToPage("/Posts/Index", new { area = "BlogAdmin" });
    }

    private async Task LoadAvailableCategoriesAsync(CancellationToken ct)
    {
        List<Category> categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        AvailableCategories = categories
            .Select(c => new CategorySelectItem(c.Id, c.Name, SelectedCategoryIds.Contains(c.Id)))
            .ToList();
    }
}

public sealed class EditPostInput
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(500, ErrorMessage = "Title must not exceed 500 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Slug is required.")]
    [MaxLength(300, ErrorMessage = "Slug must not exceed 300 characters.")]
    [RegularExpression(@"^[a-z0-9-]+$", ErrorMessage = "Slug may only contain lowercase letters, digits, and hyphens.")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Excerpt must not exceed 500 characters.")]
    public string? Excerpt { get; set; }

    [Required(ErrorMessage = "Content is required.")]
    public string Content { get; set; } = string.Empty;

    public Guid? CoverImageId { get; set; }

    public bool IsPublished { get; set; }
}
