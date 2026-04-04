using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Blogify.Web.Models;
using Blogify.Web.Models.Posts;

namespace Blogify.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public Guid? CurrentTenantId { get; set; }

        public DbSet<Tenant> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostRevision> PostRevisions { get; set; }
        public DbSet<PostCategory> PostCategories { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Media> Media { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            builder.Entity<Post>()
                .HasQueryFilter(post => post.DeletedAt == null &&
                    (!CurrentTenantId.HasValue || post.BlogId == CurrentTenantId.Value));

            builder.Entity<Category>()
                .HasQueryFilter(c => c.DeletedAt == null &&
                    (!CurrentTenantId.HasValue || c.BlogId == CurrentTenantId.Value));

            base.OnModelCreating(builder);
        }

    }
}
