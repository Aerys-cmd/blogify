---
applyTo: "**/*.cshtml,**/*.cshtml.cs"
---

# Frontend Instructions — Blogify (Razor Pages / Bootstrap 5)

## Razor Pages Structure

- Each panel is a separate Area with its own `_Layout.cshtml`:
  - `Areas/SuperAdmin/Pages/Shared/_Layout.cshtml`
  - `Areas/BlogAdmin/Pages/Shared/_Layout.cshtml`
  - `Areas/Blog/Pages/Shared/_Layout.cshtml`
- Every page that requires authentication declares `[Authorize]` or is covered by a folder-level `AuthorizeFilter` in `Program.cs`.
- `_ViewImports.cshtml` per area imports Tag Helpers, not just the root one.
- No logic in `.cshtml` files. Computed values belong in the PageModel.

---

## PageModel Rules

- Primary constructor injection only. No `[FromServices]` attribute injection in page methods.
- All public properties used in the view are initialized to non-null defaults.
- `OnGetAsync` and `OnPostAsync` inject `ApplicationDbContext` directly and orchestrate: load aggregate → call domain method → call `SaveChangesAsync` → return result.
- No separate application service layer. All orchestration lives in the PageModel.
- `RedirectToPage(...)` is the only valid navigation response from `OnPostAsync` after a successful mutation. Never return `Page()` after a successful mutation.
- Model binding uses `[BindProperty]` only on input models, never on display properties.
- Input models are `sealed record` types with `required` properties, defined at the bottom of the PageModel file.
- View models are `sealed record` types, defined at the bottom of the PageModel file.

```csharp
public sealed class CreateModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    [BindProperty]
    public required CreatePostInput Input { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        Guid blogId = tenantContext.RequiredBlogId;
        Post post = Post.Create(blogId, Input.Slug, Input.Title, Input.Content);
        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync(ct);
        return RedirectToPage("./Index");
    }

    public sealed record CreatePostInput
    {
        [Required, MaxLength(300)]
        public required string Slug { get; init; }

        [Required, MaxLength(500)]
        public required string Title { get; init; }

        [Required]
        public required string Content { get; init; }
    }
}
```

---

## Bootstrap 5 Usage

- Use Bootstrap utility classes directly in markup. Do not write custom CSS for layout or spacing unless Bootstrap cannot cover it.
- Components used must be from Bootstrap 5 only (no Bootstrap 4 patterns like `ml-*`, `mr-*` — use `ms-*`, `me-*`).
- Use `container`, `container-fluid`, or `container-{breakpoint}` — never use raw `div` with inline widths.
- Responsive grid: `col-*` classes for layout. Avoid fixed-pixel widths in Razor markup.
- Use Bootstrap's form classes: `form-control`, `form-select`, `form-check`, `form-label`, `mb-3` groups.
- Use Bootstrap alerts (`alert alert-danger`, `alert alert-success`) for user feedback messages.
- Use `btn btn-primary`, `btn btn-secondary` — no raw `<button>` without Bootstrap classes.
- Modal dialogs use Bootstrap's `modal` component with `data-bs-*` attributes, not JavaScript show/hide.

---

## Forms & Validation

- All forms use Tag Helpers: `asp-for`, `asp-action`, `asp-page`, `asp-validation-for`, `asp-validation-summary`.
- Never use `Html.TextBoxFor(...)` or other HTML Helpers.
- Validation summary appears at the top of the form: `<div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>`.
- Anti-forgery token is added automatically via `<form method="post">` with Tag Helpers. Never skip it.
- Submit buttons are disabled via `disabled` attribute or `aria-busy` during HTMX requests (see below).

---

## HTMX (Optional, Minimal)

- HTMX is permitted only for progressive enhancement: inline updates, partial refreshes, confirmation dialogs.
- Never use HTMX as a replacement for full page navigation or form submissions that change critical state.
- HTMX attributes (`hx-get`, `hx-post`, `hx-target`, `hx-swap`) are permitted on individual elements only.
- Partial responses are returned as `PartialView` from dedicated handler methods or minimal endpoints — never return a full layout in an HTMX response.
- Do not use HTMX for authentication or authorization flows.
- If a feature requires complex client-side state, escalate it as a design decision — do not reach for JavaScript or HTMX hacks.

---

## JavaScript Rules

- Vanilla JS only. No external JS frameworks (React, Vue, Alpine, etc.).
- JS is limited to: Bootstrap component initialization, minimal DOM interactions not covered by HTMX.
- No inline `onclick`, `onchange`, or `onstyle` handlers. Use `data-*` attributes and event delegation.
- Script tags go in `@section Scripts {}` blocks at page level. Never in `_Layout.cshtml` body.
- Do not use `document.write` or `eval`.

---

## Accessibility

- All form inputs have an associated `<label asp-for="...">`.
- Images have descriptive `alt` attributes or `alt=""` for decorative images.
- Buttons have either visible text or `aria-label`.
- Use semantic HTML: `<nav>`, `<main>`, `<header>`, `<footer>`, `<section>`, `<article>`.
- Color alone is never used to convey information (pair with text or icons).

---

## Partial Views & Components

- Shared partials: `Pages/Shared/_*.cshtml` (root-level).
- Area-specific partials: `Areas/{Area}/Pages/Shared/_*.cshtml`.
- Use `<partial name="_PartialName" model="..." />` — never `@Html.Partial(...)`.
- Tag Helper components are preferred over View Components unless the logic requires DI.

---

## Anti-patterns (Forbidden)

- No `ViewData["Title"]` or `ViewBag.*` usage anywhere.
- No logic-heavy `@{ ... }` blocks in `.cshtml`. Move logic to PageModel.
- No raw SQL or EF calls inside `.cshtml` files.
- No Bootstrap 4 grid or utility class patterns.
- No client-side rendering of data fetched via AJAX (use server-rendered partials via HTMX instead).
- No separate application service classes injected into PageModels — use `ApplicationDbContext` directly.
