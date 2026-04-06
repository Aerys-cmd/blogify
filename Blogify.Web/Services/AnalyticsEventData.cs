namespace Blogify.Web.Services;

/// <summary>
/// Snapshot of request data captured synchronously in the middleware and
/// placed on the analytics channel for background processing.
/// </summary>
internal sealed record AnalyticsEventData(
    Guid TenantId,
    string? PageName,
    string? Slug,
    string? Referrer,
    string? UTMSource,
    string? IpHash,
    DateTimeOffset Timestamp);
