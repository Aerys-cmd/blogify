using Microsoft.AspNetCore.Identity;

namespace Blogify.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// The ID of the tenant this user is a member of (non-owner membership).
        /// Null when the user has no blog membership (e.g. SuperAdmin or unassigned users).
        /// Ownership is tracked separately via <see cref="Tenant.OwnerId"/>.
        /// </summary>
        public Guid? TenantId { get; set; }
    }
}
