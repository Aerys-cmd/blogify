using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class BlogInvitationEntityConfiguration : IEntityTypeConfiguration<BlogInvitation>
{
    public void Configure(EntityTypeBuilder<BlogInvitation> builder)
    {
        builder.ToTable("BlogInvitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.BlogId).IsRequired();

        builder.Property(i => i.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(i => i.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(i => i.InvitedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.ExpiresAtUtc).IsRequired();
        builder.Property(i => i.AcceptedAtUtc);
        builder.Property(i => i.LastSentAtUtc);
        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(i => i.TokenHash).IsUnique();

        builder.HasIndex(i => new { i.BlogId, i.Email })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");
    }
}
