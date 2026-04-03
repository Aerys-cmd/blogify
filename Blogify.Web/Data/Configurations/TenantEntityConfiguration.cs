using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public class TenantEntityConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Blogs");

        builder.HasKey(t => t.Id);
        
        builder.Property(t => t.Id).ValueGeneratedOnAdd();
        
        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(t => t.Subdomain)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(t => t.OwnerId)
            .IsRequired();
        
        builder.HasIndex(t => t.Subdomain)
            .IsUnique();

        builder.HasOne(t => t.Owner)
            .WithMany()
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}