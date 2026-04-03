using Blogify.Web.Models;

namespace Blogify.Web.Services;

public sealed class TenantContext
{
    public Tenant? CurrentTenant { get; private set; }
    public Guid? CurrentTenantId => CurrentTenant?.Id;
    public bool IsTenantResolved => CurrentTenant is not null;

    public void Resolve(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        CurrentTenant = tenant;
    }

    public void Clear()
    {
        CurrentTenant = null;
    }
}
