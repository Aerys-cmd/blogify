using Blogify.Web.Models;
using Blogify.Web.Models.Exceptions;
using Blogify.Web.Services;

namespace Blogify.Tests;

public sealed class InvitationLifecycleTests
{
    [Fact]
    public void TokenHash_IsDeterministic_AndDoesNotExposeToken()
    {
        const string token = "secret-invitation-token";

        string hash = InvitationTokenService.Hash(token);

        Assert.Equal(hash, InvitationTokenService.Hash(token));
        Assert.Equal(64, hash.Length);
        Assert.DoesNotContain(token, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_NormalizesEmail_AndStartsPending()
    {
        BlogInvitation invitation = Create();

        Assert.Equal("invitee@example.com", invitation.Email);
        Assert.Equal(BlogInvitationStatus.Pending, invitation.Status);
        Assert.True(invitation.IsValid);
    }

    [Fact]
    public void Resend_RotatesHash_AndExtendsExpiry()
    {
        BlogInvitation invitation = Create();
        DateTimeOffset originalExpiry = invitation.ExpiresAtUtc;

        invitation.Resend("new-hash");

        Assert.Equal("new-hash", invitation.TokenHash);
        Assert.True(invitation.ExpiresAtUtc >= originalExpiry);
    }

    [Theory]
    [InlineData(BlogInvitationStatus.Accepted)]
    [InlineData(BlogInvitationStatus.Declined)]
    [InlineData(BlogInvitationStatus.Cancelled)]
    [InlineData(BlogInvitationStatus.Expired)]
    public void TerminalInvitation_CannotBeAccepted(BlogInvitationStatus status)
    {
        BlogInvitation invitation = Create();
        switch (status)
        {
            case BlogInvitationStatus.Accepted: invitation.Accept(); break;
            case BlogInvitationStatus.Declined: invitation.Decline(); break;
            case BlogInvitationStatus.Cancelled: invitation.Cancel(); break;
            case BlogInvitationStatus.Expired: invitation.Expire(); break;
        }

        Assert.Throws<DomainException>(invitation.Accept);
    }

    private static BlogInvitation Create() =>
        BlogInvitation.Create(
            Guid.NewGuid(),
            " Invitee@Example.com ",
            BlogRole.Writer,
            InvitationTokenService.Hash("token"),
            "inviter");
}
