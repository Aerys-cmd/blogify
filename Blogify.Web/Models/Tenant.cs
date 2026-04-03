using System.Text.RegularExpressions;
using Blogify.Web.Models.Exceptions;

namespace Blogify.Web.Models;

public sealed class Tenant
{
    private static readonly Regex SubdomainRegex =
        new Regex(@"^[a-z0-9-]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private Tenant() { }

    private Tenant(string title, string subdomain, string ownerId)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        Rename(title);
        ChangeSubdomain(subdomain);

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        OwnerId = ownerId;
    }

    public Guid Id { get; private init; }
    public string Title { get; private set; } = string.Empty;
    public string Subdomain { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public static Tenant Create(string title, string subdomain, string ownerId)
    {
        return new Tenant(title, subdomain, ownerId);
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Tenant title is required.", nameof(title));
        }

        string trimmed = title.Trim();
        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Tenant title must not exceed 200 characters.", nameof(title));
        }

        Title = trimmed;
    }

    public void ChangeSubdomain(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            throw new ArgumentException("Tenant subdomain is required.", nameof(subdomain));
        }

        string trimmed = subdomain.Trim().ToLowerInvariant();
        if (trimmed.Length > 63)
        {
            throw new ArgumentException("Tenant subdomain must not exceed 63 characters.", nameof(subdomain));
        }

        if (!SubdomainRegex.IsMatch(trimmed))
        {
            throw new DomainException("Tenant subdomain may only contain lowercase letters, digits, and hyphens.");
        }

        Subdomain = trimmed;
    }

    public void SoftDelete()
    {
        if (DeletedAt.HasValue)
        {
            throw new DomainException("Tenant is already deleted.");
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }
}
