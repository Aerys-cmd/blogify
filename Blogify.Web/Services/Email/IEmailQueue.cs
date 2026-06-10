namespace Blogify.Web.Services.Email;

public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default);
}
