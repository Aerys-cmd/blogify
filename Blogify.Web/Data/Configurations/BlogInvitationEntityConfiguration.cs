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

        builder.Property(i => i.Token)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(i => i.InvitedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(i => i.CreatedAtUtc).IsRequired();
        builder.Property(i => i.ExpiresAtUtc).IsRequired();
        builder.Property(i => i.AcceptedAtUtc);

        // Token must be globally unique (one-time use).
        builder.HasIndex(i => i.Token).IsUnique();

        // Pending invitations: per blog per email.
        builder.HasIndex(i => new { i.BlogId, i.Email });
    }
}
