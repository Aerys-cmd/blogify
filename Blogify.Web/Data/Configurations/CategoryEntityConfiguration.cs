using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class CategoryEntityConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.BlogId).IsRequired();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.MetaTitle)
            .HasMaxLength(60);

        builder.Property(c => c.MetaDescription)
            .HasMaxLength(160);

        builder.Property(c => c.CreatedAt).IsRequired();

        builder.Property(c => c.DeletedAt);

        builder.HasIndex(c => new { c.BlogId, c.Slug }).IsUnique();

        builder.HasIndex(c => new { c.BlogId, c.Name }).IsUnique();
    }
}

