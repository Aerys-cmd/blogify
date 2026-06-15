using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class TenantEntityConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Blogs");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Subdomain)
            .IsRequired()
            .HasMaxLength(63);

        builder.Property(t => t.OwnerId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.CreatedAt).IsRequired();

        builder.Property(t => t.DeletedAt);

        builder.Property(t => t.ActiveTheme)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("default");

        builder.Property(t => t.PublicLanguage)
            .IsRequired()
            .HasMaxLength(2)
            .HasDefaultValue("tr");

        builder.Property(t => t.MetaDescription)
            .HasMaxLength(160);

        builder.HasIndex(t => t.Subdomain).IsUnique();

        builder.HasQueryFilter(t => t.DeletedAt == null);
    }
}
