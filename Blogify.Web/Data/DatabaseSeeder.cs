using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Blogify.Web.Models;

namespace Blogify.Web.Data;

public sealed class DatabaseSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext)
{
    private const string SuperAdminEmail = "superadmin@blogify.com";
    private const string SuperAdminPassword = "SuperAdmin123A+";
    private const string BlogAdminEmail = "blogadmin@blogify.com";
    private const string BlogAdminPassword = "BlogAdmin123A+";
    private const string SuperAdminRole = "SuperAdmin";
    private const string BlogAdminRole = "BlogAdmin";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedSuperAdminAsync();
        ApplicationUser blogAdmin = await SeedBlogAdminAsync();
        await SeedTestBlogAsync(blogAdmin, cancellationToken);
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

        if (!await roleManager.RoleExistsAsync(BlogAdminRole))
        {
            IdentityResult result = await roleManager.CreateAsync(new IdentityRole(BlogAdminRole));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create '{BlogAdminRole}' role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
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

    private async Task<ApplicationUser> SeedBlogAdminAsync()
    {
        ApplicationUser? existing = await userManager.FindByEmailAsync(BlogAdminEmail);
        if (existing is not null) return existing;

        ApplicationUser user = new()
        {
            UserName = BlogAdminEmail,
            Email = BlogAdminEmail,
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(user, BlogAdminPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create BlogAdmin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(user, BlogAdminRole);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to assign '{BlogAdminRole}' role to BlogAdmin user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private async Task SeedTestBlogAsync(ApplicationUser blogAdmin, CancellationToken cancellationToken)
    {
        bool exists = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.OwnerId == blogAdmin.Id, cancellationToken);

        if (exists) return;

        Tenant testBlog = Tenant.Create("Test Blog", "test", blogAdmin.Id);
        dbContext.Blogs.Add(testBlog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

