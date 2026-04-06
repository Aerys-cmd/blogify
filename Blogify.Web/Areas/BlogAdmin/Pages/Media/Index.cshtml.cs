using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Media;

[Authorize(Roles = "BlogAdmin")]
[RequestSizeLimit(10_485_760)]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IFileStorageService fileStorage) : PageModel
{
    private const int PageSize = 24;

    public IReadOnlyList<MediaItemVm> Items { get; private set; } = [];
    public PagerVm Pager { get; private set; } = new(1, 1);

    public async Task<IActionResult> OnGetAsync(int page = 1, CancellationToken ct = default)
    {
        int totalItems = await dbContext.Media.CountAsync(ct);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        int currentPage = Math.Clamp(page, 1, totalPages);

        List<Models.Media> mediaList = await dbContext.Media
            .AsNoTracking()
            .OrderByDescending(m => m.UploadedAt)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        Items = mediaList
            .Select(m => new MediaItemVm(m.Id, m.FileName, m.Url, m.ContentType, m.SizeBytes, m.UploadedAt))
            .ToList();

        Pager = new PagerVm(currentPage, totalPages);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_MediaGrid", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnGetMediaPickerAsync(
        string targetInputId,
        string? modalId = null,
        int page = 1,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetInputId))
        {
            throw new ArgumentException("targetInputId must not be empty.", nameof(targetInputId));
        }

        string resolvedModalId = string.IsNullOrWhiteSpace(modalId)
            ? $"{targetInputId}-picker-modal"
            : modalId;

        int totalItems = await dbContext.Media.CountAsync(ct);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        int currentPage = Math.Clamp(page, 1, totalPages);

        List<Models.Media> mediaList = await dbContext.Media
            .AsNoTracking()
            .OrderByDescending(m => m.UploadedAt)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct);

        IReadOnlyList<MediaItemVm> items = mediaList
            .Select(m => new MediaItemVm(m.Id, m.FileName, m.Url, m.ContentType, m.SizeBytes, m.UploadedAt))
            .ToList();

        MediaPickerModalVm vm = new(resolvedModalId, targetInputId, items, new PagerVm(currentPage, totalPages));
        return Partial("_MediaPickerModal", vm);
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return ReturnUploadError("Please select a file to upload.");
        }

        string url;
        try
        {
            url = await fileStorage.SaveAsync(file, tenantContext.RequiredTenant.Id, ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ReturnUploadError(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ReturnUploadError(ex.Message);
        }

        Models.Media media = Models.Media.Upload(
            blogId: tenantContext.RequiredTenant.Id,
            fileName: file.FileName,
            url: url,
            contentType: file.ContentType,
            sizeBytes: file.Length);

        dbContext.Media.Add(media);
        await dbContext.SaveChangesAsync(ct);

        MediaItemVm vm = new(media.Id, media.FileName, media.Url, media.ContentType, media.SizeBytes, media.UploadedAt);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_MediaCard", vm);
        }

        return RedirectToPage("/Media/Index", new { area = "BlogAdmin" });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken ct = default)
    {
        Models.Media? media = await dbContext.Media.FirstOrDefaultAsync(m => m.Id == id, ct);

        if (media is null)
        {
            return NotFound();
        }

        await fileStorage.DeleteAsync(media.Url, ct);
        media.SoftDelete();
        await dbContext.SaveChangesAsync(ct);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return new OkResult();
        }

        return RedirectToPage("/Media/Index", new { area = "BlogAdmin" });
    }

    private IActionResult ReturnUploadError(string message)
    {
        if (Request.Headers.ContainsKey("HX-Request"))
        {
            Response.Headers["HX-Retarget"] = "#upload-error";
            Response.Headers["HX-Reswap"] = "innerHTML";
            return Content(
                $"<div class=\"alert alert-danger alert-dismissible fade show mb-0\" role=\"alert\">" +
                $"{System.Web.HttpUtility.HtmlEncode(message)}" +
                $"<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"alert\" aria-label=\"Close\"></button>" +
                $"</div>",
                "text/html");
        }

        ModelState.AddModelError("file", message);
        return Page();
    }
}

public sealed record MediaItemVm(Guid Id, string FileName, string Url, string ContentType, long SizeBytes, DateTimeOffset UploadedAt);

public sealed record PagerVm(int CurrentPage, int TotalPages);

public sealed record MediaPickerFieldVm(string InputId, string InputName, string? CurrentUrl, string ModalId);

public sealed record MediaPickerModalVm(string ModalId, string TargetInputId, IReadOnlyList<MediaItemVm> Items, PagerVm Pager);
