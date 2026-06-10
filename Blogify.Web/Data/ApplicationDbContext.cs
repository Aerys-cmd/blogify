using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
        public DbSet<BlogMembership> BlogMemberships { get; set; }
        public DbSet<BlogInvitation> BlogInvitations { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostRevision> PostRevisions { get; set; }
        public DbSet<PostCategory> PostCategories { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Media> Media { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            builder.Entity<Post>()
                .HasQueryFilter(post => post.DeletedAt == null &&
                    (!CurrentTenantId.HasValue || post.BlogId == CurrentTenantId.Value));

            builder.Entity<Category>()
                .HasQueryFilter(c => c.DeletedAt == null &&
                    (!CurrentTenantId.HasValue || c.BlogId == CurrentTenantId.Value));

            builder.Entity<Media>()
                .HasQueryFilter(m => m.DeletedAt == null &&
                    (!CurrentTenantId.HasValue || m.BlogId == CurrentTenantId.Value));

            builder.Entity<Comment>()
                .HasQueryFilter(c => c.DeletedAt == null &&
                    (!CurrentTenantId.HasValue || c.BlogId == CurrentTenantId.Value));

            base.OnModelCreating(builder);

            ValueConverter<DateTimeOffset, long> dateTimeOffsetConverter = new(
                value => value.UtcTicks,
                value => new DateTimeOffset(value, TimeSpan.Zero));

            ValueConverter<DateTimeOffset?, long?> nullableDateTimeOffsetConverter = new(
                value => value.HasValue ? value.Value.UtcTicks : null,
                value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

            foreach (IMutableEntityType entityType in builder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(dateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(nullableDateTimeOffsetConverter);
                    }
                }
            }
        }

    }
}
