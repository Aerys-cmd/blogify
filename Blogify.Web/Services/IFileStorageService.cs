using Microsoft.AspNetCore.Http;

namespace Blogify.Web.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(IFormFile file, Guid tenantId, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}

