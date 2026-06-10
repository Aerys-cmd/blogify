namespace Blogify.Web.Services.Email;

public sealed class DisabledEmailDeliveryTransport(
    ILogger<DisabledEmailDeliveryTransport> logger) : IEmailDeliveryTransport
{
    public Task DeliverAsync(EmailJob job, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Email delivery is disabled. Discarding email to {Recipient} with subject {Subject}.",
            job.Recipient,
            job.Subject);
        return Task.CompletedTask;
    }
}
