namespace Blogify.Web.Models;

public enum AnalyticsEventType
{
    PageView
}

public sealed class AnalyticsEvent
{
    private AnalyticsEvent() { }

    private AnalyticsEvent(
        Guid tenantId,
        Guid? postId,
        AnalyticsEventType eventType,
        string? referrer,
        string? utmSource,
        string? ipHash,
        DateTimeOffset timestamp)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId), "Tenant id must not be empty.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        PostId = postId;
        EventType = eventType;
        Referrer = referrer;
        UTMSource = utmSource;
        Timestamp = timestamp;
        IpHash = ipHash;
    }

    public Guid Id { get; private init; }
    public Guid TenantId { get; private init; }
    public Guid? PostId { get; private init; }
    public AnalyticsEventType EventType { get; private init; }
    public string? Referrer { get; private init; }
    public string? UTMSource { get; private init; }
    public DateTimeOffset Timestamp { get; private init; }
    public string? IpHash { get; private init; }

    public static AnalyticsEvent Create(
        Guid tenantId,
        Guid? postId,
        AnalyticsEventType eventType,
        string? referrer,
        string? utmSource,
        string? ipHash,
        DateTimeOffset? timestamp = null)
    {
        return new AnalyticsEvent(tenantId, postId, eventType, referrer, utmSource, ipHash,
            timestamp ?? DateTimeOffset.UtcNow);
    }
}
