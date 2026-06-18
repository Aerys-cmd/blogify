using System.Globalization;
using Blogify.Web.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Services.Email;

public sealed class AppEmailSender(
    IRazorEmailRenderer renderer,
    IEmailQueue queue,
    IOptions<EmailOptions> options) : IAppEmailSender
{
    public async Task SendPasswordResetAsync(
        string recipient,
        string token,
        CultureInfo culture,
        CancellationToken ct = default)
    {
        string language = GetLanguage(culture);
        string resetUrl = BuildUrl(
            "/reset-password",
            new Dictionary<string, string?>
            {
                ["token"] = token,
                ["email"] = recipient,
            });
        string subject = language == "tr" ? "Blogify şifrenizi sıfırlayın" : "Reset your Blogify password";
        string html = await renderer.RenderAsync(
            $"/Emails/PasswordReset.{language}.cshtml",
            new PasswordResetEmailViewModel(resetUrl));

        await queue.EnqueueAsync(new EmailJob(recipient, subject, html), ct);
    }

    public async Task SendBlogInvitationAsync(
        string recipient,
        string blogTitle,
        BlogRole role,
        string token,
        CultureInfo culture,
        CancellationToken ct = default)
    {
        string language = GetLanguage(culture);
        string invitationUrl = BuildUrl($"/invite/{Uri.EscapeDataString(token)}");
        string localizedRole = language == "tr"
            ? role switch
            {
                BlogRole.Admin => "Yönetici",
                BlogRole.Writer => "Yazar",
                _ => role.ToString(),
            }
            : role.ToString();
        string subject = language == "tr"
            ? $"{blogTitle} bloguna davet edildiniz"
            : $"You are invited to {blogTitle}";
        string html = await renderer.RenderAsync(
            $"/Emails/BlogInvitation.{language}.cshtml",
            new BlogInvitationEmailViewModel(blogTitle, localizedRole, invitationUrl));

        await queue.EnqueueAsync(new EmailJob(recipient, subject, html), ct);
    }

    private string BuildUrl(string path, IReadOnlyDictionary<string, string?>? query = null)
    {
        string baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        string url = $"{baseUrl}/{path.TrimStart('/')}";
        return query is null ? url : QueryHelpers.AddQueryString(url, query);
    }

    private static string GetLanguage(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, "tr", StringComparison.OrdinalIgnoreCase)
            ? "tr"
            : "en";
}
