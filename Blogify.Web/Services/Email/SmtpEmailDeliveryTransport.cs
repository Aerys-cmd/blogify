using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Blogify.Web.Services.Email;

public sealed class SmtpEmailDeliveryTransport(
    IOptions<EmailOptions> emailOptions,
    IOptions<SmtpOptions> smtpOptions) : IEmailDeliveryTransport
{
    public async Task DeliverAsync(EmailJob job, CancellationToken ct = default)
    {
        EmailOptions email = emailOptions.Value;
        SmtpOptions smtp = smtpOptions.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        message.To.Add(MailboxAddress.Parse(job.Recipient));
        message.Subject = job.Subject;
        message.Body = new BodyBuilder { HtmlBody = job.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = ServerCertificateValidation;

        SecureSocketOptions socketOptions = smtp.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, ct);
        if (!string.IsNullOrWhiteSpace(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    private static bool ServerCertificateValidation(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
            return true;

        if (errors == SslPolicyErrors.RemoteCertificateChainErrors && chain != null)
        {
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                    status.Status == X509ChainStatusFlags.OfflineRevocation)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        return false;
    }


}
