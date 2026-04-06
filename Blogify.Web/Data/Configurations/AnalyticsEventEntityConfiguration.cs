using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class AnalyticsEventEntityConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.ToTable("AnalyticsEvents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.TenantId).IsRequired();

        builder.Property(e => e.PostId);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Referrer).HasMaxLength(2048);

        builder.Property(e => e.UTMSource).HasMaxLength(255);

        builder.Property(e => e.Timestamp).IsRequired();

        builder.Property(e => e.IpHash).HasMaxLength(64);

        builder.HasIndex(e => new { e.TenantId, e.Timestamp });

        builder.HasIndex(e => new { e.TenantId, e.PostId, e.Timestamp });
    }
}
