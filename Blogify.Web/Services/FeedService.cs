using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Blogify.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Blogify.Web.Services;

public sealed class FeedService(ApplicationDbContext dbContext, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly Regex HtmlTagRegex =
        new(@"<[^>]+>", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    public void InvalidateTenant(Guid tenantId)
    {
        cache.Remove(SitemapCacheKey(tenantId));
        cache.Remove(RssCacheKey(tenantId));
    }

    public async Task<string> GetSitemapAsync(Guid tenantId, string baseUrl, CancellationToken ct)
    {
        string key = SitemapCacheKey(tenantId);
        string? xml = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await BuildSitemapAsync(tenantId, baseUrl, ct);
        });
        return xml ?? string.Empty;
    }

    public async Task<string> GetRssAsync(Guid tenantId, string blogTitle, string baseUrl, CancellationToken ct)
    {
        string key = RssCacheKey(tenantId);
        string? xml = await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await BuildRssAsync(tenantId, blogTitle, baseUrl, ct);
        });
        return xml ?? string.Empty;
    }

    private async Task<string> BuildSitemapAsync(Guid tenantId, string baseUrl, CancellationToken ct)
    {
        dbContext.CurrentTenantId = tenantId;
        List<SitemapRow> posts = await (
            from p in dbContext.Posts.AsNoTracking()
            where p.PublishedRevisionId != null
            join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
            orderby r.CreatedAt descending
            select new SitemapRow(p.Slug, r.CreatedAt)
        ).ToListAsync(ct);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XDocument doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                posts.Select(p => new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}/{p.Slug}"),
                    new XElement(ns + "lastmod", p.PublishedAt.ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.8")
                ))
            )
        );

        return ToXmlString(doc);
    }

    private async Task<string> BuildRssAsync(Guid tenantId, string blogTitle, string baseUrl, CancellationToken ct)
    {
        dbContext.CurrentTenantId = tenantId;
        List<RssRow> posts = await (
            from p in dbContext.Posts.AsNoTracking()
            where p.PublishedRevisionId != null
            join r in dbContext.PostRevisions.AsNoTracking() on p.PublishedRevisionId equals r.Id
            orderby r.CreatedAt descending
            select new RssRow(p.Slug, r.Title, p.Excerpt, r.Content, r.CreatedAt)
        ).Take(20).ToListAsync(ct);

        XDocument doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XElement("channel",
                    new XElement("title", blogTitle),
                    new XElement("link", baseUrl),
                    new XElement("description", $"{blogTitle} RSS Feed"),
                    posts.Select(p =>
                    {
                        string description = !string.IsNullOrWhiteSpace(p.Excerpt)
                            ? p.Excerpt
                            : StripHtmlAndTruncate(p.Content, 300);

                        return new XElement("item",
                            new XElement("title", p.Title),
                            new XElement("link", $"{baseUrl}/{p.Slug}"),
                            new XElement("description", description),
                            new XElement("pubDate", p.PublishedAt.ToString("R")),
                            new XElement("guid",
                                new XAttribute("isPermaLink", "true"),
                                $"{baseUrl}/{p.Slug}")
                        );
                    })
                )
            )
        );

        return ToXmlString(doc);
    }

    private static string ToXmlString(XDocument doc)
    {
        using MemoryStream ms = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true
        }))
        {
            doc.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string StripHtmlAndTruncate(string html, int maxLength)
    {
        string stripped = HtmlTagRegex.Replace(html, string.Empty).Trim();
        return stripped.Length <= maxLength ? stripped : stripped[..maxLength].TrimEnd() + "\u2026";
    }

    private static string SitemapCacheKey(Guid tenantId) => $"feed:sitemap:{tenantId}";
    private static string RssCacheKey(Guid tenantId) => $"feed:rss:{tenantId}";

    private sealed record SitemapRow(string Slug, DateTimeOffset PublishedAt);

    private sealed record RssRow(
        string Slug,
        string Title,
        string? Excerpt,
        string Content,
        DateTimeOffset PublishedAt);
}
