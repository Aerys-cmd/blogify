namespace Blogify.Web.Services.Email;

public sealed class EmailDispatchWorker(
    EmailQueue queue,
    IEmailDeliveryTransport transport,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(30),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (EmailJob job in queue.Reader.ReadAllAsync(stoppingToken))
            await DeliverAsync(job, stoppingToken);
    }

    private async Task DeliverAsync(EmailJob job, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await transport.DeliverAsync(job, ct);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < RetryDelays.Length)
            {
                TimeSpan delay = RetryDelays[attempt];
                logger.LogWarning(
                    ex,
                    "Email delivery to {Recipient} failed. Retrying in {DelaySeconds} seconds.",
                    job.Recipient,
                    delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Email delivery to {Recipient} failed after all retries. Discarding message.",
                    job.Recipient);
                return;
            }
        }
    }
}
