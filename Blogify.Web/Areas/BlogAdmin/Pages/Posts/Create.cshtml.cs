using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Posts;

[Authorize(Roles = "BlogAdmin")]
public sealed class CreateModel(ApplicationDbContext dbContext, TenantContext tenantContext, FeedService feedService) : PageModel
{
    [BindProperty]
    public CreatePostInput Input { get; set; } = new();

    [BindProperty]
    public List<Guid> SelectedCategoryIds { get; set; } = [];

    public IReadOnlyList<CategorySelectItem> AvailableCategories { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
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

        string? authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(authorId))
        {
            ModelState.AddModelError(string.Empty, "Unable to determine the current user. Please sign in again.");
            await LoadAvailableCategoriesAsync(ct);
            return Page();
        }

        bool slugTaken = await dbContext.Posts
            .AsNoTracking()
            .AnyAsync(p => p.Slug == Input.Slug.Trim().ToLowerInvariant(), ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), "This slug is already in use for this blog.");
            await LoadAvailableCategoriesAsync(ct);
            return Page();
        }

        Guid blogId = tenantContext.RequiredTenant.Id;

        Post post = Post.Create(
            blogId: blogId,
            authorId: authorId,
            slug: Input.Slug,
            initialTitle: Input.Title,
            initialContent: Input.Content
        );

        post.UpdateExcerpt(Input.Excerpt);
        post.SetCoverImage(Input.CoverImageId);
        post.SetCategories(SelectedCategoryIds);

        if (Input.Publish)
        {
            Guid initialRevisionId = post.Revisions[0].Id;
            post.Publish(initialRevisionId);
        }

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(ct);

        if (Input.Publish)
        {
            feedService.InvalidateTenant(blogId);
        }

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

public sealed class CreatePostInput
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

    public bool Publish { get; set; }
}

public sealed record CategorySelectItem(Guid Id, string Name, bool IsSelected);
