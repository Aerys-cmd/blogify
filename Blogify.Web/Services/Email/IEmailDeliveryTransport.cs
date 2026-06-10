namespace Blogify.Web.Services.Email;

public interface IEmailDeliveryTransport
{
    Task DeliverAsync(EmailJob job, CancellationToken ct = default);
}
