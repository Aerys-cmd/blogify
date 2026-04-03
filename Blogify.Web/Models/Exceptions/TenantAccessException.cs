namespace Blogify.Web.Models.Exceptions;

public sealed class TenantAccessException : Exception
{
    public TenantAccessException(string message) : base(message) { }
}

