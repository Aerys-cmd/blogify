using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Posts;

[Authorize(Roles = "BlogAdmin,SuperAdmin")]
public sealed class CreateModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    [BindProperty]
    public CreatePostInput Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> CategoryOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
    {
        if (!tenantContext.IsTenantResolved || tenantContext.CurrentTenant is null)
        {
            return NotFound("Blog admin area requires a tenant host (for example: yourblog.localhost).");
        }

        await LoadCategoryOptionsAsync(tenantContext.CurrentTenant.Id, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
    {
        if (!tenantContext.IsTenantResolved || tenantContext.CurrentTenant is null)
        {
            return NotFound("Blog admin area requires a tenant host (for example: yourblog.localhost).");
        }

        Guid blogId = tenantContext.CurrentTenant.Id;

        if (!ModelState.IsValid)
        {
            await LoadCategoryOptionsAsync(blogId, ct);
            return Page();
        }

        string? authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(authorId))
        {
            ModelState.AddModelError(string.Empty, "Unable to determine the current user. Please sign in again.");
            await LoadCategoryOptionsAsync(blogId, ct);
            return Page();
        }

        bool slugTaken = await dbContext.Posts
            .AsNoTracking()
            .AnyAsync(p => p.Slug == Input.Slug.Trim().ToLowerInvariant(), ct);

        if (slugTaken)
        {
            ModelState.AddModelError(nameof(Input.Slug), "This slug is already in use for this blog.");
            await LoadCategoryOptionsAsync(blogId, ct);
            return Page();
        }

        Post post = Post.Create(
            blogId: blogId,
            authorId: authorId,
            slug: Input.Slug,
            initialTitle: Input.Title,
            initialContent: Input.Content
        );

        post.UpdateExcerpt(Input.Excerpt);
        post.UpdateFeaturedImageUrl(Input.FeaturedImageUrl);
        post.AssignCategory(Input.CategoryId);

        if (Input.Publish)
        {
            Guid initialRevisionId = post.Revisions[0].Id;
            post.Publish(initialRevisionId);
        }

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("/Posts/Index", new { area = "BlogAdmin" });
    }

    private async Task LoadCategoryOptionsAsync(Guid blogId, CancellationToken ct)
    {
        List<Category> categories = await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.BlogId == blogId && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        CategoryOptions = categories
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
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

    public Guid? CategoryId { get; set; }

    [MaxLength(2048, ErrorMessage = "Featured image URL must not exceed 2048 characters.")]
    public string? FeaturedImageUrl { get; set; }

    public bool Publish { get; set; }
}

