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

        builder.Property(m => m.AltText).HasMaxLength(500);

        builder.Property(m => m.Title).HasMaxLength(255);

        builder.Property(m => m.Description).HasMaxLength(2000);

        builder.Property(m => m.ThumbnailUrl);

        builder.Property(m => m.WidthPx);

        builder.Property(m => m.HeightPx);

        builder.HasIndex(m => new { m.BlogId, m.UploadedAt })
            .HasDatabaseName("IX_Media_BlogId_UploadedAt");
    }
}

