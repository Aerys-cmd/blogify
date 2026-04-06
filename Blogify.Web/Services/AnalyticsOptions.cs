namespace Blogify.Web.Services;

public sealed class AnalyticsOptions
{
    /// <summary>
    /// Salt/pepper used to HMAC-SHA256-hash IP addresses before storing them.
    /// Configure this per environment (e.g. via secrets or env vars).
    /// If left empty a random per-process key is generated at startup, meaning
    /// hashes will change on restart (approximate deduplication still works within
    /// a single process lifetime).
    /// </summary>
    public string IpHashSalt { get; set; } = string.Empty;
}
