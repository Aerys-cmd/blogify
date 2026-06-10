using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class BlogMembershipEntityConfiguration : IEntityTypeConfiguration<BlogMembership>
{
    public void Configure(EntityTypeBuilder<BlogMembership> builder)
    {
        builder.ToTable("BlogMemberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.BlogId).IsRequired();

        builder.Property(m => m.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(m => m.InvitedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(m => m.JoinedAtUtc).IsRequired();

        // Unique: one membership record per user per blog.
        builder.HasIndex(m => new { m.BlogId, m.UserId }).IsUnique();
    }
}
