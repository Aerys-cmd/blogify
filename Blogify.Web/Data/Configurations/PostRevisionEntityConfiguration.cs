using Blogify.Web.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public class PostRevisionEntityConfiguration : IEntityTypeConfiguration<PostRevision>
{
    public void Configure(EntityTypeBuilder<PostRevision> builder)
    {
        builder.ToTable("PostRevisions");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .IsRequired();

        builder.Property(r => r.PostId)
            .IsRequired();

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Content)
            .IsRequired();

        builder.Property(r => r.SeoTitle)
            .HasMaxLength(200);

        builder.Property(r => r.SeoKeywords)
            .HasMaxLength(500);

        builder.Property(r => r.SeoDescription)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.ModifiedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.PostId, r.CreatedAt });
    }
}

