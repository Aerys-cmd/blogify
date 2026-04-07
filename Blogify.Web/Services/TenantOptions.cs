namespace Blogify.Web.Services;

public sealed class TenantOptions
{
    public string[] PlatformHosts { get; init; } = ["localhost", "saasplatform.local"];
}
