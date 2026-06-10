using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Blogify.Web.Models;

namespace Blogify.Web.Data;

public sealed class DatabaseSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext)
{
    private const string SuperAdminEmail    = "superadmin@blogify.com";
    private const string SuperAdminPassword = "SuperAdmin123A+";
    private const string UserEmail          = "user@blogify.com";
    private const string UserPassword       = "User1234A+";
    private const string SuperAdminRole     = "SuperAdmin";
    private const string UserRole           = "User";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync();
        await SeedSuperAdminAsync();
        ApplicationUser seedUser = await SeedUserAsync();
        await SeedTestBlogAsync(seedUser, cancellationToken);
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
        ApplicationUser? existing = await userManager.FindByEmailAsync(UserEmail);
        if (existing is not null) return existing;

        ApplicationUser user = new()
        {
            UserName = UserEmail,
            Email = UserEmail,
            EmailConfirmed = true
        };

        IdentityResult result = await userManager.CreateAsync(user, UserPassword);
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

    private async Task SeedTestBlogAsync(ApplicationUser owner, CancellationToken cancellationToken)
    {
        bool exists = await dbContext.Blogs
            .AsNoTracking()
            .AnyAsync(b => b.OwnerId == owner.Id, cancellationToken);

        if (exists) return;

        Tenant testBlog = Tenant.Create("Test Blog", "test", owner.Id);
        dbContext.Blogs.Add(testBlog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
