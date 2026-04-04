using System.ComponentModel.DataAnnotations;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Posts;

[Authorize(Roles = "BlogAdmin")]
public sealed class EditModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public EditPostInput Input { get; set; } = new();

    [BindProperty]
    public List<Guid> SelectedCategoryIds { get; set; } = [];

    public IReadOnlyList<CategorySelectItem> AvailableCategories { get; private set; } = [];
    public string PostTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        Post? post = await dbContext.Posts
            .Include(p => p.Revisions)
            .Include(p => p.Categories)
            .FirstOrDefaultAsync(p => p.Id == Id, ct);

        if (post is null || post.DeletedAt.HasValue)
        {
            return NotFound();
        }

        PostRevision latestRevision = post.Revisions
            .OrderByDescending(r => r.CreatedAt)
            .First();

        PostTitle = latestRevision.Title;
        SelectedCategoryIds = post.Categories.Select(pc => pc.CategoryId).ToList();

        Input = new EditPostInput
        {
            Title = latestRevision.Title,
            Slug = post.Slug,
            Excerpt = post.Excerpt,
            Content = latestRevision.Content,
            FeaturedImageUrl = post.FeaturedImageUrl,
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
            ModelState.AddModelError(nameof(Input.Slug), "This slug is already in use for this blog.");
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
        post.UpdateFeaturedImageUrl(Input.FeaturedImageUrl);
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


    [MaxLength(2048, ErrorMessage = "Featured image URL must not exceed 2048 characters.")]
    public string? FeaturedImageUrl { get; set; }

    public bool IsPublished { get; set; }
}
