using Blogify.Web.Models.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class PostCategoryConfiguration : IEntityTypeConfiguration<PostCategory>
{
    public void Configure(EntityTypeBuilder<PostCategory> builder)
    {
        builder.ToTable("PostCategories");

        builder.HasKey(pc => new { pc.PostId, pc.CategoryId });

        builder.HasOne(pc => pc.Post)
            .WithMany(p => p.Categories)
            .HasForeignKey(pc => pc.PostId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(pc => pc.Post).IsRequired(false);

        builder.HasOne(pc => pc.Category)
            .WithMany()
            .HasForeignKey(pc => pc.CategoryId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(pc => pc.Category).IsRequired(false);
    }
}



