using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Blogify.Web.Services.Email;

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailJob> _channel;

    public EmailQueue(IOptions<EmailOptions> options)
    {
        int capacity = Math.Max(1, options.Value.QueueCapacity);
        _channel = Channel.CreateBounded<EmailJob>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ValueTask EnqueueAsync(EmailJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    internal ChannelReader<EmailJob> Reader => _channel.Reader;
}
