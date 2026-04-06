using Blogify.Web.Data;
using Blogify.Web.Models;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Analytics;

[Authorize(Roles = "BlogAdmin")]
public sealed class IndexModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    public int TotalViews7Days { get; private set; }
    public int TotalViews30Days { get; private set; }
    public int UniqueVisitors7Days { get; private set; }
    public int UniqueVisitors30Days { get; private set; }
    public IReadOnlyList<TopPostViewModel> TopPosts { get; private set; } = [];
    public IReadOnlyList<TopReferrerViewModel> TopReferrers { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Guid tenantId = tenantContext.RequiredTenant.Id;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset cutoff7 = now.AddDays(-7);
        DateTimeOffset cutoff30 = now.AddDays(-30);

        IQueryable<AnalyticsEvent> tenantEvents = dbContext.AnalyticsEvents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EventType == AnalyticsEventType.PageView);

        TotalViews7Days = await tenantEvents
            .CountAsync(e => e.Timestamp >= cutoff7, ct);

        TotalViews30Days = await tenantEvents
            .CountAsync(e => e.Timestamp >= cutoff30, ct);

        UniqueVisitors7Days = await tenantEvents
            .Where(e => e.Timestamp >= cutoff7 && e.IpHash != null)
            .Select(e => e.IpHash)
            .Distinct()
            .CountAsync(ct);

        UniqueVisitors30Days = await tenantEvents
            .Where(e => e.Timestamp >= cutoff30 && e.IpHash != null)
            .Select(e => e.IpHash)
            .Distinct()
            .CountAsync(ct);

        List<TopPostRow> topPostRows = await tenantEvents
            .Where(e => e.Timestamp >= cutoff30 && e.PostId.HasValue)
            .GroupBy(e => e.PostId!.Value)
            .Select(g => new TopPostRow(g.Key, g.Count()))
            .OrderByDescending(r => r.Views)
            .Take(5)
            .ToListAsync(ct);

        if (topPostRows.Count > 0)
        {
            List<Guid> postIds = topPostRows.Select(r => r.PostId).ToList();

            Dictionary<Guid, string> postTitles = await (
                from p in dbContext.Posts.AsNoTracking()
                where postIds.Contains(p.Id) && p.PublishedRevisionId != null
                join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
                select new { p.Id, r.Title }
            ).ToDictionaryAsync(x => x.Id, x => x.Title, ct);

            TopPosts = topPostRows
                .Select(r => new TopPostViewModel(
                    postTitles.GetValueOrDefault(r.PostId, "(deleted post)"),
                    r.Views))
                .ToList();
        }

        TopReferrers = await tenantEvents
            .Where(e => e.Timestamp >= cutoff30 && e.Referrer != null)
            .Select(e => e.Referrer ?? string.Empty)
            .GroupBy(referrer => referrer)
            .Select(g => new TopReferrerViewModel(g.Key, g.Count()))
            .OrderByDescending(r => r.Views)
            .Take(10)
            .ToListAsync(ct);
    }

    private sealed record TopPostRow(Guid PostId, int Views);
}

public sealed record TopPostViewModel(string Title, int Views);

public sealed record TopReferrerViewModel(string Referrer, int Views);
