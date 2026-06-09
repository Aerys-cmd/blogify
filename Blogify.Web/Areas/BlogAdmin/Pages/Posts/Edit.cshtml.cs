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
    public string PostCreatedAtFormatted { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public string? SavedToastType { get; private set; }

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
        PostCreatedAtFormatted = post.CreatedAt.ToString("MMM d, yyyy");

        AuthorName = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == post.AuthorId)
            .Select(u => u.UserName!)
            .FirstOrDefaultAsync(ct) ?? post.AuthorId;

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
            IsPublished = post.Status == PostStatus.Published,
            MetaTitle = post.MetaTitle,
            MetaDescription = post.MetaDescription
        };

        SavedToastType = TempData["PostSaved"] as string;

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
            string contentText = BlockNoteContentExtractor.ExtractPlainText(Input.Content);
            post.AddRevision(Input.Title, Input.Content, contentText);
        }

        post.UpdateExcerpt(Input.Excerpt);
        post.SetCoverImage(Input.CoverImageId);
        post.SetCategories(SelectedCategoryIds);
        post.UpdateSeoMetadata(Input.MetaTitle, Input.MetaDescription);

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

        TempData["PostSaved"] = (Input.IsPublished && !currentlyPublished) ? "published"
            : (!Input.IsPublished && currentlyPublished) ? "unpublished"
            : "saved";

        return RedirectToPage("/Posts/Edit", new { area = "BlogAdmin", id = Id });
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
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    [RegularExpression(@"^[a-z0-9-]+$")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Excerpt { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public Guid? CoverImageId { get; set; }

    public bool IsPublished { get; set; }

    [MaxLength(60)]
    public string? MetaTitle { get; set; }

    [MaxLength(160)]
    public string? MetaDescription { get; set; }
}
