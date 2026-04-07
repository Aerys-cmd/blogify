---
applyTo: "**/*.resx,**/*.cshtml,**/*.cshtml.cs"
---

# Translation & Localization Instructions — Blogify

## Overview

- Supported cultures: `en` (default), `tr`.
- Resource files live in `Blogify.Web/Resources/`:
  - `SharedResource.resx` — English (default, neutral culture).
  - `SharedResource.tr.resx` — Turkish.
- The `SharedResource` marker class is in `Blogify.Web` namespace (`Services/SharedResource.cs`). **Never move it to a sub-namespace.** ASP.NET Core resolves the resource file path from the type's namespace relative to the assembly root — moving it would break all lookups.
- `ResourcesPath` is configured as `"Resources"` in `Program.cs`. The marker class namespace must match this path exactly (i.e., `Blogify.Web.SharedResource` → `Resources/SharedResource.resx`).

---

## Using the Localizer in Views

- `IHtmlLocalizer<SharedResource>` is injected as `@Localizer` in `Pages/_ViewImports.cshtml` and is available in every view and partial under `Pages/` and all Areas.
- **Never hard-code user-visible strings in `.cshtml` files.** Always use `@Localizer["Key"]`.
- For attribute values (e.g., `placeholder`, `aria-label`, `title`) use `@Localizer["Key"].Value` to get the raw string without HTML encoding.

```razor
{{!-- ✅ Correct --}}
<h2>@Localizer["Index.Hero.Heading"]</h2>
<input placeholder="@Localizer["Label.Email"].Value" />

{{!-- ❌ Wrong — hard-coded English --}}
<h2>Your story. Your blog. Your way.</h2>
```

---

## Using the Localizer in PageModels

- Inject `IStringLocalizer<SharedResource>` (not `IHtmlLocalizer`) into PageModels that need localized strings (e.g., for validation error messages).
- `SharedResource` resolves from parent-namespace lookup — no explicit `using Blogify.Web;` is needed inside any `Blogify.Web.*` namespace.

```csharp
// ✅ Correct
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        ModelState.AddModelError(string.Empty, localizer["Message.SubdomainTaken"].Value);
        return Page();
    }
}
```

---

## Resource Key Naming Convention

Keys follow a dot-separated `Section.Subsection.Name` pattern. Always add new keys to **both** resx files.

| Prefix | Purpose | Examples |
|---|---|---|
| `Label.*` | Form field labels | `Label.Email`, `Label.Password`, `Label.Title` |
| `Action.*` | Buttons and links | `Action.Save`, `Action.Delete`, `Action.SignIn` |
| `Validation.*` | Validation messages | `Validation.Required`, `Validation.EmailInvalid` |
| `Page.Title.*` | `<title>` and page headings | `Page.Title.BlogAdmin.Posts`, `Page.Title.Landing.Home` |
| `Message.*` | Success/error feedback | `Message.SaveSuccess`, `Message.SubdomainTaken` |
| `Nav.*` | Navigation items | `Nav.Posts`, `Nav.Dashboard`, `Nav.SignOut` |
| `Identity.*` | ASP.NET Identity error descriptions | `Identity.PasswordTooShort`, `Identity.DuplicateEmail` |
| `Lang.*` | Language switcher labels | `Lang.English`, `Lang.Turkish` |
| `GetStarted.*` | GetStarted wizard pages | `GetStarted.Step1.Heading`, `GetStarted.Step2.Submit` |
| `Index.*` | Landing index page sections | `Index.Hero.Heading`, `Index.Features.Subdomain.Title` |

- Sub-sections follow the page or feature name: `Index.Hero.*`, `Index.Features.Subdomain.*`, `GetStarted.Step1.*`.
- New page sections get their own prefix (e.g., `Privacy.*`, `Error.*`).
- Never reuse a key for two semantically different strings even if the English values happen to be identical.

---

## Adding a New Translated String — Checklist

1. Add the key and English value to `SharedResource.resx`.
2. Add the key and Turkish value to `SharedResource.tr.resx`.
3. Replace the hard-coded string in the view with `@Localizer["The.New.Key"]`.
4. Both files must stay in sync — every key present in `.resx` must also be present in `.tr.resx`.

---

## Culture Switching

- Culture is persisted via a cookie set by `POST /culture` (`CultureEndpoints`).
- The `_LanguageSwitcher.cshtml` partial renders the switcher buttons and is included in `_LandingLayout.cshtml` (and any other layout that needs it).
- `app.UseAntiforgery()` must appear in `Program.cs` **after** `app.UseAuthorization()` and **before** `app.MapStaticAssets()` — the culture endpoint carries anti-forgery metadata.
- The cookie provider is `CookieRequestCultureProvider` with cookie name `.AspNetCore.Culture`. Do not change the cookie name.
- Valid culture values: `"en"`, `"tr"`. Any other value is rejected with `400 Bad Request` by the endpoint.

---

## DataAnnotations Localization

- DataAnnotations validation messages are localized automatically via `AddDataAnnotationsLocalization` in `Program.cs`, which delegates to `IStringLocalizerFactory.Create(typeof(SharedResource))`.
- Do **not** set `ErrorMessage` on `[Required]`, `[MaxLength]`, `[EmailAddress]`, etc. — the shared localizer provides the messages.
- `LocalizedIdentityErrorDescriber` (in `Blogify.Web.Services`) overrides all ASP.NET Core Identity error messages using `Identity.*` keys.

---

## Anti-patterns (Forbidden)

- Do not hard-code any user-visible English string in a `.cshtml` file.
- Do not add a key to only one resx file — both must be updated together.
- Do not create a separate localizer class or resource file per page or feature. The single `SharedResource` is the source of truth for all translations.
- Do not use `IStringLocalizer<T>` with any type other than `SharedResource`.
- Do not move the `SharedResource` class out of the `Blogify.Web` namespace.
- Do not use `CultureInfo.CurrentCulture.Name` to conditionally render content — always go through `@Localizer`.

