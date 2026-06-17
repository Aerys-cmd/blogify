using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Blogify.Web.Services;

namespace Blogify.Web.Data;

public sealed class DatabaseSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext,
    IWebHostEnvironment environment)
{
    private const string SuperAdminEmail    = "superadmin@blogify.com";
    private const string SuperAdminPassword = "SuperAdmin123A+";
    private const string UserEmail          = "user@blogify.com";
    private const string UserPassword       = "User1234A+";
    private const string SuperAdminRole     = "SuperAdmin";
    private const string UserRole           = "User";
    private const string EditorEmail        = "editor@blogify.com";
    private const string EditorPassword     = "Editor1234A+";
    private const string ReaderEmail        = "reader@blogify.com";
    private const string ReaderPassword     = "Reader1234A+";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedSuperAdminAsync();
        ApplicationUser seedUser = await SeedUserAsync();
        Tenant testBlog = await SeedTestBlogAsync(seedUser, cancellationToken);

        if (environment.IsDevelopment())
        {
            await SeedDevelopmentContentAsync(seedUser, testBlog, cancellationToken);
        }
    }

    private async Task SeedRolesAsync()
    {
        if (!await roleManager.RoleExistsAsync(SuperAdminRole))
        {
            IdentityResult result = await roleManager.CreateAsync(new IdentityRole(SuperAdminRole));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create '{SuperAdminRole}' role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        if (!await roleManager.RoleExistsAsync(UserRole))
        {
            IdentityResult result = await roleManager.CreateAsync(new IdentityRole(UserRole));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create '{UserRole}' role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        ApplicationUser? existing = await userManager.FindByEmailAsync(SuperAdminEmail);
        if (existing is not null) return;

        ApplicationUser user = new()
        {
            UserName = SuperAdminEmail,
            Email = SuperAdminEmail,
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(user, SuperAdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create SuperAdmin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, SuperAdminRole);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign '{SuperAdminRole}' role to SuperAdmin user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }
    }

    private async Task<ApplicationUser> SeedUserAsync()
    {
        return await SeedUserAsync(UserEmail, UserPassword);
    }

    private async Task<ApplicationUser> SeedUserAsync(string email, string password)
    {
        ApplicationUser? existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        ApplicationUser user = new()
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create seed user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, UserRole);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign '{UserRole}' role to seed user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private async Task<Tenant> SeedTestBlogAsync(ApplicationUser owner, CancellationToken cancellationToken)
    {
        Tenant? existing = await dbContext.Blogs
            .FirstOrDefaultAsync(b => b.OwnerId == owner.Id && b.Subdomain == "test", cancellationToken);

        if (existing is not null)
        {
            existing.Rename("Northstar Notes");
            existing.ChangeTheme("aurora");
            existing.ChangePublicLanguage("en");
            existing.UpdateSeoMetadata(
                "Northstar Notes",
                "Practical field notes on building calmer products, teams, and systems.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        Tenant testBlog = Tenant.Create("Northstar Notes", "test", owner.Id);
        testBlog.ChangeTheme("aurora");
        testBlog.ChangePublicLanguage("en");
        testBlog.UpdateSeoMetadata(
            "Northstar Notes",
            "Practical field notes on building calmer products, teams, and systems.");
        dbContext.Blogs.Add(testBlog);
        await dbContext.SaveChangesAsync(cancellationToken);

        return testBlog;
    }

    private async Task SeedDevelopmentContentAsync(ApplicationUser owner, Tenant blog, CancellationToken cancellationToken)
    {
        Guid? previousTenantId = dbContext.CurrentTenantId;
        dbContext.CurrentTenantId = blog.Id;

        try
        {
        ApplicationUser editor = await SeedUserAsync(EditorEmail, EditorPassword);
        ApplicationUser reader = await SeedUserAsync(ReaderEmail, ReaderPassword);

        bool editorIsMember = await dbContext.BlogMemberships
            .AsNoTracking()
            .AnyAsync(m => m.BlogId == blog.Id && m.UserId == editor.Id, cancellationToken);

        if (!editorIsMember)
        {
            dbContext.BlogMemberships.Add(BlogMembership.Create(blog.Id, editor.Id, BlogRole.Editor, owner.Id));
        }

        List<Category> categories =
        [
            await EnsureCategoryAsync(blog.Id, "Product", "product", cancellationToken),
            await EnsureCategoryAsync(blog.Id, "Engineering", "engineering", cancellationToken),
            await EnsureCategoryAsync(blog.Id, "Operations", "operations", cancellationToken),
            await EnsureCategoryAsync(blog.Id, "Customer Stories", "customer-stories", cancellationToken)
        ];

        categories[0].UpdateSeoMetadata("Product articles", "Product strategy, research, and launch notes.");
        categories[1].UpdateSeoMetadata("Engineering articles", "Engineering systems, reliability, and delivery notes.");

        List<Tag> tags =
        [
            await EnsureTagAsync(blog.Id, "SaaS", "saas", cancellationToken),
            await EnsureTagAsync(blog.Id, "Roadmap", "roadmap", cancellationToken),
            await EnsureTagAsync(blog.Id, "Research", "research", cancellationToken),
            await EnsureTagAsync(blog.Id, "Reliability", "reliability", cancellationToken),
            await EnsureTagAsync(blog.Id, "Teams", "teams", cancellationToken),
            await EnsureTagAsync(blog.Id, "Launch", "launch", cancellationToken)
        ];

        List<Media> media =
        [
            await EnsureSeedImageAsync(blog.Id, "workspace-planning.jpg", "https://images.unsplash.com/photo-1497366754035-f200968a6e72", "Team planning product work around a conference table.", cancellationToken),
            await EnsureSeedImageAsync(blog.Id, "deploy-dashboard.jpg", "https://images.unsplash.com/photo-1551288049-bebda4e38f71", "Analytics dashboard on a workstation.", cancellationToken),
            await EnsureSeedImageAsync(blog.Id, "customer-research.jpg", "https://images.unsplash.com/photo-1552664730-d307ca884978", "Customer research interview notes and laptops.", cancellationToken),
            await EnsureSeedImageAsync(blog.Id, "support-queue.jpg", "https://images.unsplash.com/photo-1551434678-e076c223a692", "People collaborating around a support queue.", cancellationToken)
        ];

        blog.UpdateBranding(media[0].Id, null, media[1].Id);

        Post? launchPlan = await EnsurePublishedPostAsync(
            blog.Id,
            owner.Id,
            "how-we-plan-a-calm-launch",
            "How we plan a calm launch",
            "Launches get messy when every decision is saved for the final week. Our team uses a calmer launch rhythm that makes risk visible early and keeps the last day boring.",
            media[0],
            [
                ("heading", "Start with the support load", 2),
                ("paragraph", "Before we talk about banners, release notes, or launch emails, we write down the questions customers are most likely to ask. That list shapes docs, onboarding, and internal checklists.", null),
                ("paragraph", "The goal is not to remove every surprise. It is to make sure the predictable work is already assigned.", null),
                ("heading", "Keep the launch window small", 2),
                ("paragraph", "A focused launch window helps marketing, support, engineering, and product look at the same signals at the same time. When something is unclear, the right people are already in the room.", null)
            ],
            [categories[0].Id, categories[2].Id],
            [tags[1].Id, tags[5].Id, tags[4].Id],
            cancellationToken);

        Post? reliability = await EnsurePublishedPostAsync(
            blog.Id,
            editor.Id,
            "the-weekly-reliability-review",
            "The weekly reliability review",
            "A practical look at the lightweight review we use to keep incidents, slow queries, and noisy alerts from becoming background radiation.",
            media[1],
            [
                ("heading", "Review the boring metrics first", 2),
                ("paragraph", "Error rates, queue depth, latency, and failed jobs are not glamorous, but they give the fastest read on whether the product feels trustworthy.", null),
                ("paragraph", "We keep the review short and write down only decisions that change ownership or priority.", null),
                ("heading", "Close the loop", 2),
                ("paragraph", "Every review ends with one owner, one due date, and one sentence explaining what will be different when the work is done.", null)
            ],
            [categories[1].Id, categories[2].Id],
            [tags[3].Id, tags[4].Id, tags[0].Id],
            cancellationToken);

        await EnsurePublishedPostAsync(
            blog.Id,
            owner.Id,
            "customer-research-that-survives-the-roadmap",
            "Customer research that survives the roadmap",
            "Research loses value when it becomes a slide deck nobody revisits. Here is how we turn interview notes into product bets the whole team can inspect.",
            media[2],
            [
                ("heading", "Write observations, not verdicts", 2),
                ("paragraph", "A good research note keeps the customer's words close to the surface. That makes it easier for engineering, design, and support to challenge weak assumptions.", null),
                ("paragraph", "The roadmap gets healthier when decisions can be traced back to concrete moments in the conversation.", null),
                ("heading", "Connect each insight to a next action", 2),
                ("paragraph", "Every insight should point to a change: a prototype, an experiment, a docs update, or a deliberate decision to wait.", null)
            ],
            [categories[0].Id, categories[3].Id],
            [tags[2].Id, tags[1].Id, tags[0].Id],
            cancellationToken);

        await EnsureDraftPostAsync(blog.Id, editor.Id, media[3], categories[0].Id, [tags[0].Id, tags[2].Id], cancellationToken);

        if (launchPlan is not null)
        {
            await EnsureCommentsAsync(blog.Id, launchPlan, reliability, owner.Id, reader.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.CurrentTenantId = previousTenantId;
        }
    }

    private async Task<Category> EnsureCategoryAsync(Guid blogId, string name, string slug, CancellationToken cancellationToken)
    {
        Category? existing = await dbContext.Categories
            .FirstOrDefaultAsync(c => c.BlogId == blogId && c.Slug == slug, cancellationToken);

        if (existing is not null)
        {
            existing.Update(name, slug);
            return existing;
        }

        Category category = Category.Create(blogId, name, slug);
        dbContext.Categories.Add(category);
        return category;
    }

    private async Task<Tag> EnsureTagAsync(Guid blogId, string name, string slug, CancellationToken cancellationToken)
    {
        Tag? existing = await dbContext.Tags
            .FirstOrDefaultAsync(t => t.BlogId == blogId && t.Slug == slug, cancellationToken);

        if (existing is not null)
        {
            existing.Rename(name);
            return existing;
        }

        Tag tag = Tag.Create(blogId, name, slug);
        dbContext.Tags.Add(tag);
        return tag;
    }

    private async Task<Media> EnsureSeedImageAsync(Guid blogId, string fileName, string url, string altText, CancellationToken cancellationToken)
    {
        Media? existing = await dbContext.Media
            .FirstOrDefaultAsync(m => m.BlogId == blogId && m.FileName == fileName, cancellationToken);

        if (existing is not null)
        {
            existing.UpdateMetadata(altText, Path.GetFileNameWithoutExtension(fileName).Replace('-', ' '), "Development seed image.");
            return existing;
        }

        Media media = CreateSeedImage(blogId, fileName, url, altText);
        dbContext.Media.Add(media);
        return media;
    }

    private static Media CreateSeedImage(Guid blogId, string fileName, string url, string altText)
    {
        Media media = Media.Upload(blogId, fileName, url, "image/jpeg", 240_000);
        media.UpdateMetadata(altText, Path.GetFileNameWithoutExtension(fileName).Replace('-', ' '), "Development seed image.");
        return media;
    }

    private async Task<Post?> EnsurePublishedPostAsync(
        Guid blogId,
        string authorId,
        string slug,
        string title,
        string excerpt,
        Media coverImage,
        IReadOnlyList<(string Type, string Text, int? Level)> blocks,
        IEnumerable<Guid> categoryIds,
        IEnumerable<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        Post? existing = await dbContext.Posts
            .FirstOrDefaultAsync(p => p.BlogId == blogId && p.Slug == slug, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        string content = CreateContent(blocks, coverImage);
        string contentText = BlockNoteContentExtractor.ExtractPlainText(content);
        Post post = Post.Create(blogId, authorId, slug, title, content, contentText);

        post.UpdateExcerpt(excerpt);
        post.SetCoverImage(coverImage.Id);
        post.SetCategories(categoryIds);
        post.SetTags(tagIds);
        post.UpdateSeoMetadata(title.Length <= 60 ? title : title[..60], excerpt.Length <= 160 ? excerpt : excerpt[..160]);
        post.Publish(post.Revisions[0].Id);

        dbContext.Posts.Add(post);
        return post;
    }

    private async Task EnsureDraftPostAsync(
        Guid blogId,
        string authorId,
        Media coverImage,
        Guid categoryId,
        IEnumerable<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        bool exists = await dbContext.Posts
            .AsNoTracking()
            .AnyAsync(p => p.BlogId == blogId && p.Slug == "draft-pricing-page-refresh", cancellationToken);

        if (exists) return;

        Post draft = Post.Create(
            blogId,
            authorId,
            "draft-pricing-page-refresh",
            "Draft: Pricing page refresh",
            CreateContent([
                ("heading", "What the pricing page needs to answer", 2),
                ("paragraph", "This draft collects notes for a clearer pricing page: who each plan is for, how limits are explained, and where procurement details should live.", null)
            ], coverImage),
            "This draft collects notes for a clearer pricing page: who each plan is for, how limits are explained, and where procurement details should live.");
        draft.UpdateExcerpt("A work-in-progress article kept unpublished so the admin post list has realistic draft content.");
        draft.SetCoverImage(coverImage.Id);
        draft.SetCategories([categoryId]);
        draft.SetTags(tagIds);
        draft.UpdateSeoMetadata("Pricing page refresh", "Draft notes for improving a SaaS pricing page.");

        dbContext.Posts.Add(draft);
    }

    private async Task EnsureCommentsAsync(
        Guid blogId,
        Post launchPlan,
        Post? reliability,
        string ownerId,
        string readerId,
        CancellationToken cancellationToken)
    {
        bool launchHasComments = await dbContext.Comments
            .AsNoTracking()
            .AnyAsync(c => c.PostId == launchPlan.Id, cancellationToken);

        if (!launchHasComments)
        {
            Comment firstComment = Comment.Create(
                blogId,
                launchPlan.Id,
                readerId,
                "This is the kind of launch checklist I wish more teams shared. The support-load section is especially useful.");
            firstComment.Approve(ownerId);

            Comment reply = Comment.Create(
                blogId,
                launchPlan.Id,
                ownerId,
                "That part came from a painful launch where we shipped the feature before the internal answers were ready.",
                firstComment.Id);
            reply.Approve(ownerId);

            dbContext.Comments.AddRange(firstComment, reply);
        }

        if (reliability is null) return;

        bool reliabilityHasComments = await dbContext.Comments
            .AsNoTracking()
            .AnyAsync(c => c.PostId == reliability.Id, cancellationToken);

        if (reliabilityHasComments) return;

        Comment pending = Comment.Create(
            blogId,
            reliability.Id,
            readerId,
            "Can you share a template for the weekly review agenda?");

        dbContext.Comments.Add(pending);
    }

    private static string CreateContent(IReadOnlyList<(string Type, string Text, int? Level)> blocks, Media? image = null)
    {
        List<object> contentBlocks = [];

        if (image is not null)
        {
            contentBlocks.Add(new
            {
                id = Guid.NewGuid().ToString("N"),
                type = "image",
                props = new
                {
                    url = image.Url,
                    caption = image.AltText ?? image.FileName,
                    previewWidth = 960
                },
                content = Array.Empty<object>(),
                children = Array.Empty<object>()
            });
        }

        foreach ((string type, string text, int? level) in blocks)
        {
            contentBlocks.Add(new
            {
                id = Guid.NewGuid().ToString("N"),
                type,
                props = type == "heading" ? new { level = level ?? 2 } : new { level = 0 },
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text,
                        styles = new { }
                    }
                },
                children = Array.Empty<object>()
            });
        }

        return System.Text.Json.JsonSerializer.Serialize(contentBlocks);
    }
}
