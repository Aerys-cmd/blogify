using System.Globalization;
using Blogify.Web.Models;

namespace Blogify.Web.Services.Email;

public interface IAppEmailSender
{
    Task SendPasswordResetAsync(
        string recipient,
        string token,
        CultureInfo culture,
        CancellationToken ct = default);

    Task SendBlogInvitationAsync(
        string recipient,
        string blogTitle,
        BlogRole role,
        string token,
        CultureInfo culture,
        CancellationToken ct = default);
}
