using Blogify.Web.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public class PostEntityConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TenantId)
            .IsRequired();

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.PublishedRevisionId);

        builder.Property(p => p.DraftRevisionId);

        builder.HasIndex(p => new { p.TenantId, p.Slug })
            .IsUnique();

        builder.HasMany(p => p.Revisions)
            .WithOne(r => r.Post)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

