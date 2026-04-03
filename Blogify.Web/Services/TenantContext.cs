using Blogify.Web.Models;

namespace Blogify.Web.Services
{
    public class TenantContext
    {
        public Tenant? CurrentTenant { get; private set; }
        public int? CurrentTenantId => CurrentTenant?.Id;
        public bool IsTenantResolved => CurrentTenant != null;

        public void Resolve(Tenant tenant)
        {
            CurrentTenant = tenant;
        }

        public void Clear()
        {
            CurrentTenant = null;
        }
    }
}
