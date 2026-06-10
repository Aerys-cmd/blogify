using Microsoft.AspNetCore.Identity;

namespace Blogify.Web.Models
{
    /// <summary>
    /// Application user. Every registered account is simply a User.
    /// Blog access is controlled via <see cref="BlogMembership"/> (multi-blog) and
    /// <see cref="Tenant.OwnerId"/> (ownership). No TenantId on the user record.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
    }
}
