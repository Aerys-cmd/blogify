using Blogify.Web.Data;
using Blogify.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Services;

/// <summary>
/// Hosted service that drains the <see cref="AnalyticsChannel"/> and persists
/// events to the database. Runs as a single background loop so DB writes are
/// sequential and never saturate the connection pool.
/// </summary>
public sealed class AnalyticsWriterService(
    AnalyticsChannel analyticsChannel,
    IServiceScopeFactory scopeFactory,
    ILogger<AnalyticsWriterService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (AnalyticsEventData data in analyticsChannel.Reader.ReadAllAsync(stoppingToken))
        {
            await WriteEventAsync(data, stoppingToken);
        }
    }

    private async Task WriteEventAsync(AnalyticsEventData data, CancellationToken ct)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CurrentTenantId = data.TenantId;

            Guid? postId = null;
            if (string.Equals(data.PageName, "/Post", StringComparison.OrdinalIgnoreCase) &&
                data.Slug is not null)
            {
                postId = await db.Posts
                    .AsNoTracking()
                    .Where(p => p.Slug == data.Slug && p.PublishedRevisionId != null)
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(ct);
            }

            AnalyticsEvent analyticsEvent = AnalyticsEvent.Create(
                data.TenantId,
                postId,
                AnalyticsEventType.PageView,
                data.Referrer,
                data.UTMSource,
                data.IpHash,
                data.Timestamp);

            db.AnalyticsEvents.Add(analyticsEvent);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to write analytics event for tenant {TenantId}.", data.TenantId);
        }
    }
}
