using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blogify.Web.Data.Configurations;

public sealed class BlogInvitationEventEntityConfiguration : IEntityTypeConfiguration<BlogInvitationEvent>
{
    public void Configure(EntityTypeBuilder<BlogInvitationEvent> builder)
    {
        builder.ToTable("BlogInvitationEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.ActorUserId).HasMaxLength(450);
        builder.Property(e => e.Details).HasMaxLength(500);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.HasIndex(e => e.InvitationId);
    }
}
