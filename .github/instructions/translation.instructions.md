---
applyTo: "**/*.resx,**/*.cshtml,**/*.cshtml.cs"
---

# Localization Rules

- Supported cultures are `en` and `tr`.
- Shared resources are `Blogify.Web/Resources/SharedResource.resx` and `Blogify.Web/Resources/SharedResource.tr.resx`.
- Add, rename, or remove keys in both files in the same change.
- User-visible Razor text uses `@Localizer["Key"]`; use `.Value` where a raw attribute string is required.
- C# code that needs localized text uses `IStringLocalizer<SharedResource>`.
- Identity validation messages are localized through `LocalizedIdentityErrorDescriber`.
- Public blogs use the owner's configured public language via `UsePublicBlogCulture`; do not branch UI text manually on the current culture.
- Keep `SharedResource` in the `Blogify.Web` namespace; moving it breaks resource lookup.
- Reuse a key only when its meaning is identical. Prefer short, feature-oriented dot-separated names.
