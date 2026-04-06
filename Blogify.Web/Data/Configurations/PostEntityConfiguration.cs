using Blogify.Web.Models;
using Blogify.Web.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class PostEntityConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.BlogId).IsRequired();

        builder.Property(p => p.AuthorId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(p => p.Excerpt)
            .HasMaxLength(500);

        builder.Property(p => p.CoverImageId);

        builder.HasOne<Media>()
            .WithMany()
            .HasForeignKey(p => p.CoverImageId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(p => p.PublishedRevisionId);

        builder.Property(p => p.CreatedAt).IsRequired();

        builder.Property(p => p.DeletedAt);

        builder.HasIndex(p => new { p.BlogId, p.Slug }).IsUnique();

        builder.HasMany(p => p.Revisions)
            .WithOne()
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
