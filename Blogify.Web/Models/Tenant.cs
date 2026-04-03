namespace Blogify.Web.Models;

public class Tenant
{
    private Tenant()
    {
    }

    private Tenant(string title, string subdomain, string ownerId)
    {
        Rename(title);
        ChangeSubdomain(subdomain);
        SetOwner(ownerId);
    }

    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Subdomain { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty;

    public ApplicationUser? Owner { get; private set; }

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

        Title = title.Trim();
    }

    public void ChangeSubdomain(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
        {
            throw new ArgumentException("Tenant subdomain is required.", nameof(subdomain));
        }

        Subdomain = subdomain.Trim().ToLowerInvariant();
    }

    public void SetOwner(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(ownerId));
        }

        OwnerId = ownerId;
    }
}
