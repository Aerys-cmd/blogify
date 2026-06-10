namespace Blogify.Web.Models;

/// <summary>
/// The role a user holds within a specific blog (membership role).
/// Owner is tracked separately via <see cref="Tenant.OwnerId"/> and is not included here.
/// </summary>
public enum BlogRole
{
    Writer = 1,
    Editor = 2,
    Admin  = 3
}
