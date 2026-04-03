using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class MediaEntityConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("Media");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.BlogId).IsRequired();

        builder.Property(m => m.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.Url).IsRequired();

        builder.Property(m => m.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.SizeBytes).IsRequired();

        builder.Property(m => m.UploadedAt).IsRequired();

        builder.Property(m => m.DeletedAt);

        builder.HasQueryFilter(m => m.DeletedAt == null);
    }
}

