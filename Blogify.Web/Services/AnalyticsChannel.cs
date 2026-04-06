using System.Threading.Channels;

namespace Blogify.Web.Services;

/// <summary>
/// Singleton bounded channel that decouples the request-path analytics
/// capture from the background DB write. Capacity is capped to prevent
/// unbounded memory growth under load; excess events are dropped.
/// </summary>
public sealed class AnalyticsChannel
{
    private const int Capacity = 10_000;

    private readonly Channel<AnalyticsEventData> _channel =
        Channel.CreateBounded<AnalyticsEventData>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropNewest,
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>Attempts to enqueue an event. Returns false and discards if the channel is full.</summary>
    internal bool TryEnqueue(AnalyticsEventData data) => _channel.Writer.TryWrite(data);

    internal ChannelReader<AnalyticsEventData> Reader => _channel.Reader;
}
