using Blogify.Web.Data;
using Blogify.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Blogify.Web.Areas.BlogAdmin.Pages.Media;

[Authorize]
[RequestSizeLimit(10_485_760)]
public sealed class IndexModel(
    ApplicationDbContext dbContext,
    TenantContext tenantContext,
    IFileStorageService fileStorage,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    private const int PageSize = 24;
    private const int ThumbnailMaxWidthPx = 300;

    public IReadOnlyList<MediaItemVm> Items { get; private set; } = [];
    public CursorPagerVm Pager { get; private set; } = CursorPagerVm.Empty;
    public string? SearchQuery { get; private set; }
    public string? TypeFilter { get; private set; }
    public string? MonthFilter { get; private set; }
    public string ViewMode { get; private set; } = "grid";
    public IReadOnlyList<MonthBucketVm> AvailableMonths { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(
        string? cursor = null,
        string? q = null,
        string? type = null,
        string? month = null,
        string view = "grid",
        CancellationToken ct = default)
    {
        SearchQuery = q;
        TypeFilter = type;
        MonthFilter = month;
        ViewMode = view;

        (IReadOnlyList<Models.Media> mediaList, string? nextCursor) =
            await FetchPageAsync(cursor, q, type, month, ct);

        Items = mediaList
            .Select(m => new MediaItemVm(
                m.Id, m.FileName, m.Url, m.ContentType, m.SizeBytes,
                m.UploadedAt, m.AltText, m.Title, m.ThumbnailUrl))
            .ToList();

        Pager = new CursorPagerVm(nextCursor);

        AvailableMonths = await LoadAvailableMonthsAsync(ct);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_MediaGrid", this);
        }

        return Page();
    }

    public async Task<IActionResult> OnGetSearchAsync(
        string? q = null,
        string? type = null,
        string? month = null,
        string cursor = "",
        string view = "grid",
        CancellationToken ct = default)
    {
        SearchQuery = q;
        TypeFilter = type;
        MonthFilter = month;
        ViewMode = view;

        (IReadOnlyList<Models.Media> mediaList, string? nextCursor) =
            await FetchPageAsync(string.IsNullOrWhiteSpace(cursor) ? null : cursor, q, type, month, ct);

        Items = mediaList
            .Select(m => new MediaItemVm(
                m.Id, m.FileName, m.Url, m.ContentType, m.SizeBytes,
                m.UploadedAt, m.AltText, m.Title, m.ThumbnailUrl))
            .ToList();

        Pager = new CursorPagerVm(nextCursor);

        return Partial("_MediaGrid", this);
    }

    public async Task<IActionResult> OnGetMediaPickerAsync(
        string targetInputId,
        string? modalId = null,
        string? cursor = null,
        string? q = null,
        string? type = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetInputId))
        {
            throw new ArgumentException("targetInputId must not be empty.", nameof(targetInputId));
        }

        string resolvedModalId = string.IsNullOrWhiteSpace(modalId)
            ? $"{targetInputId}-picker-modal"
            : modalId;

        (IReadOnlyList<Models.Media> mediaList, string? nextCursor) =
            await FetchPageAsync(cursor, q, type, null, ct);

        IReadOnlyList<MediaItemVm> items = mediaList
            .Select(m => new MediaItemVm(
                m.Id, m.FileName, m.Url, m.ContentType, m.SizeBytes,
                m.UploadedAt, m.AltText, m.Title, m.ThumbnailUrl))
            .ToList();

        MediaPickerModalVm vm = new(resolvedModalId, targetInputId, items, new CursorPagerVm(nextCursor), type, q);
        return Partial("_MediaPickerModal", vm);
    }

    public async Task<IActionResult> OnGetMediaPickerPageAsync(
        string targetInputId,
        string? modalId = null,
        string? cursor = null,
        string? q = null,
        string? type = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetInputId))
        {
            return BadRequest();
        }

        string resolvedModalId = string.IsNullOrWhiteSpace(modalId)
            ? $"{targetInputId}-picker-modal"
            : modalId;

        (IReadOnlyList<Models.Media> mediaList, string? nextCursor) =
            await FetchPageAsync(cursor, q, type, null, ct);

        IReadOnlyList<MediaItemVm> items = mediaList
            .Select(m => new MediaItemVm(
                m.Id, m.FileName, m.Url, m.ContentType, m.SizeBytes,
                m.UploadedAt, m.AltText, m.Title, m.ThumbnailUrl))
            .ToList();

        MediaPickerPageVm vm = new(resolvedModalId, targetInputId, items, new CursorPagerVm(nextCursor), type, q);
        return Partial("_MediaPickerPage", vm);
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            return ReturnUploadError(localizer["Message.UploadFileRequired"].Value);
        }

        Models.Media media;
        try
        {
            media = await SaveUploadedFileAsync(file, ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ReturnUploadError(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ReturnUploadError(ex.Message);
        }

        MediaItemVm vm = new(
            media.Id, media.FileName, media.Url, media.ContentType,
            media.SizeBytes, media.UploadedAt, media.AltText, media.Title, media.ThumbnailUrl);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_MediaCard", vm);
        }

        return RedirectToPage("/Media/Index", new { area = "BlogAdmin" });
    }

    public async Task<IActionResult> OnPostPickerUploadAsync(
        IFormFile? file,
        string targetInputId,
        string? modalId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetInputId) || !IsSafeDomId(targetInputId))
        {
            return BadRequest();
        }

        string resolvedModalId = !string.IsNullOrWhiteSpace(modalId) && IsSafeDomId(modalId)
            ? modalId
            : $"{targetInputId}-picker-modal";

        if (file is null || file.Length == 0)
        {
            return ReturnPickerUploadError(localizer["Message.UploadFileRequired"].Value, resolvedModalId);
        }

        Models.Media media;
        try
        {
            media = await SaveUploadedFileAsync(file, ct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ReturnPickerUploadError(ex.Message, resolvedModalId);
        }
        catch (ArgumentException ex)
        {
            return ReturnPickerUploadError(ex.Message, resolvedModalId);
        }

        MediaItemVm item = new(
            media.Id, media.FileName, media.Url, media.ContentType,
            media.SizeBytes, media.UploadedAt, media.AltText, media.Title, media.ThumbnailUrl);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_MediaPickerCard", new MediaPickerCardVm(item, targetInputId, resolvedModalId));
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

    public async Task<IActionResult> OnPostBulkDeleteAsync(
        [FromForm] List<Guid> ids,
        CancellationToken ct = default)
    {
        if (ids is null || ids.Count == 0)
        {
            return RedirectToPage("/Media/Index", new { area = "BlogAdmin" });
        }

        List<Models.Media> mediaItems = await dbContext.Media
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);

        foreach (Models.Media media in mediaItems)
        {
            await fileStorage.DeleteAsync(media.Url, ct);
            media.SoftDelete();
        }

        await dbContext.SaveChangesAsync(ct);

        return RedirectToPage("/Media/Index", new { area = "BlogAdmin" });
    }

    public async Task<IActionResult> OnPostUpdateMetadataAsync(
        Guid id,
        string? altText,
        string? title,
        string? description,
        CancellationToken ct = default)
    {
        Models.Media? media = await dbContext.Media.FirstOrDefaultAsync(m => m.Id == id, ct);

        if (media is null)
        {
            return NotFound();
        }

        try
        {
            media.UpdateMetadata(altText, title, description);
        }
        catch (ArgumentException ex)
        {
            if (Request.Headers.ContainsKey("HX-Request"))
            {
                Response.StatusCode = 422;
                return Content(
                    $"<div class=\"alert alert-danger mb-0\">{System.Web.HttpUtility.HtmlEncode(ex.Message)}</div>",
                    "text/html");
            }

            return BadRequest(ex.Message);
        }

        await dbContext.SaveChangesAsync(ct);

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Content(
                "<div class=\"alert alert-success mb-0\">Metadata saved.</div>",
                "text/html");
        }

        return RedirectToPage("/Media/Index", new { area = "BlogAdmin" });
    }

    private static string EncodeCursor(DateTimeOffset uploadedAt, Guid id)
    {
        string raw = $"{uploadedAt:O}|{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTimeOffset UploadedAt, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            string raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            string[] parts = raw.Split('|');
            if (parts.Length != 2)
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(parts[0], out DateTimeOffset uploadedAt))
            {
                return null;
            }

            if (!Guid.TryParse(parts[1], out Guid id))
            {
                return null;
            }

            return (uploadedAt, id);
        }
        catch
        {
            return null;
        }
    }

    private async Task<(IReadOnlyList<Models.Media> Items, string? NextCursor)> FetchPageAsync(
        string? cursor,
        string? q,
        string? type,
        string? month,
        CancellationToken ct)
    {
        IQueryable<Models.Media> query = dbContext.Media.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            string pattern = $"%{q.Trim()}%";
            query = query.Where(m => EF.Functions.Like(m.FileName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
        {
            string prefix = type == "image" ? "image/" : "application/";
            query = query.Where(m => m.ContentType.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(month) &&
            DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out DateOnly monthStart))
        {
            DateTimeOffset from = new(monthStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            DateTimeOffset to = from.AddMonths(1);
            query = query.Where(m => m.UploadedAt >= from && m.UploadedAt < to);
        }

        (DateTimeOffset UploadedAt, Guid Id)? decoded = DecodeCursor(cursor);
        if (decoded.HasValue)
        {
            DateTimeOffset ts = decoded.Value.UploadedAt;
            Guid id = decoded.Value.Id;
            query = query.Where(m => m.UploadedAt < ts || (m.UploadedAt == ts && m.Id.CompareTo(id) < 0));
        }

        List<Models.Media> items = await query
            .OrderByDescending(m => m.UploadedAt)
            .ThenByDescending(m => m.Id)
            .Take(PageSize + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (items.Count > PageSize)
        {
            items.RemoveAt(PageSize);
            Models.Media last = items[^1];
            nextCursor = EncodeCursor(last.UploadedAt, last.Id);
        }

        return (items, nextCursor);
    }

    private async Task<Models.Media> SaveUploadedFileAsync(IFormFile file, CancellationToken ct)
    {
        string url = await fileStorage.SaveAsync(file, tenantContext.RequiredTenant.Id, ct);

        Models.Media media = Models.Media.Upload(
            blogId: tenantContext.RequiredTenant.Id,
            fileName: file.FileName,
            url: url,
            contentType: file.ContentType,
            sizeBytes: file.Length);

        if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            (int Width, int Height)? dims =
                await fileStorage.GetImageDimensionsAsync(url, ct);

            string? thumbnailUrl =
                await fileStorage.SaveThumbnailAsync(url, tenantContext.RequiredTenant.Id, ThumbnailMaxWidthPx, ct);

            if (thumbnailUrl is not null && dims.HasValue)
            {
                media.SetThumbnail(thumbnailUrl, dims.Value.Width, dims.Value.Height);
            }
        }

        dbContext.Media.Add(media);
        await dbContext.SaveChangesAsync(ct);

        return media;
    }

    private static bool IsSafeDomId(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Za-z0-9_\-]+$");

    private IActionResult ReturnUploadError(string message, string? htmxRetargetId = null)
    {
        if (Request.Headers.ContainsKey("HX-Request"))
        {
            Response.Headers["HX-Retarget"] = htmxRetargetId ?? "#upload-error";
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

    private IActionResult ReturnPickerUploadError(string message, string modalId) =>
        ReturnUploadError(message, $"#picker-upload-error-{modalId}");

    public async Task<IActionResult> OnGetMediaDetailAsync(Guid id, CancellationToken ct = default)
    {
        Models.Media? media = await dbContext.Media
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (media is null)
        {
            return NotFound();
        }

        List<string> usedInPosts = await dbContext.Posts
            .AsNoTracking()
            .Where(p => p.CoverImageId == id)
            .Select(p => dbContext.PostRevisions
                .Where(r => r.PostId == p.Id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.Title)
                .FirstOrDefault() ?? p.Slug)
            .ToListAsync(ct);

        MediaDetailVm vm = new(
            media.Id, media.FileName, media.Url, media.ContentType,
            media.SizeBytes, media.UploadedAt, media.AltText, media.Title,
            media.Description, media.ThumbnailUrl, media.WidthPx, media.HeightPx,
            usedInPosts);

        return Partial("_MediaDetailOffcanvas", vm);
    }

    private async Task<IReadOnlyList<MonthBucketVm>> LoadAvailableMonthsAsync(CancellationToken ct)
    {
        List<DateTimeOffset> uploadedDates = await dbContext.Media
            .AsNoTracking()
            .Select(m => m.UploadedAt)
            .ToListAsync(ct);

        return uploadedDates
            .Select(uploadedAt => new { uploadedAt.Year, uploadedAt.Month })
            .Distinct()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new MonthBucketVm(
                $"{x.Year}-{x.Month:D2}",
                $"{new DateTime(x.Year, x.Month, 1):MMMM yyyy}"))
            .ToList();
    }
}

public sealed record MediaItemVm(
    Guid Id,
    string FileName,
    string Url,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string? AltText,
    string? Title,
    string? ThumbnailUrl);

public sealed record CursorPagerVm(string? NextCursor)
{
    public static readonly CursorPagerVm Empty = new CursorPagerVm(NextCursor: null);
}

public sealed record MediaPickerFieldVm(
    string InputId,
    string InputName,
    Guid? CurrentMediaId,
    string? CurrentThumbnailUrl,
    string ModalId,
    string? ModalTitle = null,
    string? RecommendedDimensionsHint = null);

public sealed record MediaPickerModalVm(
    string ModalId,
    string TargetInputId,
    IReadOnlyList<MediaItemVm> Items,
    CursorPagerVm Pager,
    string? TypeFilter = null,
    string? SearchQuery = null);

public sealed record MonthBucketVm(string Value, string Label);

public sealed record MediaPickerCardVm(
    MediaItemVm Item,
    string TargetInputId,
    string ModalId);

public sealed record MediaPickerPageVm(
    string ModalId,
    string TargetInputId,
    IReadOnlyList<MediaItemVm> Items,
    CursorPagerVm Pager,
    string? TypeFilter = null,
    string? SearchQuery = null);

public sealed record MediaDetailVm(
    Guid Id,
    string FileName,
    string Url,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    string? AltText,
    string? Title,
    string? Description,
    string? ThumbnailUrl,
    int? WidthPx,
    int? HeightPx,
    IReadOnlyList<string> UsedInPosts);
