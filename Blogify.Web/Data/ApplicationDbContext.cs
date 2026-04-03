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

        public int? CurrentTenantId { get; set; }

        public DbSet<Tenant> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostRevision> PostRevisions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            builder.Entity<Post>()
                .HasQueryFilter(post => CurrentTenantId.HasValue && post.TenantId == CurrentTenantId.Value);

            builder.Entity<PostRevision>()
                .HasQueryFilter(revision => CurrentTenantId.HasValue && revision.TenantId == CurrentTenantId.Value);

            base.OnModelCreating(builder);
        }

    }
}
