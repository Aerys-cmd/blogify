namespace Blogify.Web.Services.Email;

public sealed record PasswordResetEmailViewModel(string ResetUrl);

public sealed record BlogInvitationEmailViewModel(
    string BlogTitle,
    string Role,
    string InvitationUrl);
