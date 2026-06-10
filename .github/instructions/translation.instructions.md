---
applyTo: "**/*.resx,**/*.cshtml,**/*.cshtml.cs"
---

# Localization Rules

- Supported cultures are `en` and `tr`.
- Shared resources are `Blogify.Web/Resources/SharedResource.resx` and `SharedResource.tr.resx`.
- Add, rename, or remove keys in both files in the same change.
- User-visible Razor text uses `@Localizer["Key"]`; use `.Value` where a raw attribute string is required.
- C# code that needs localized text uses `IStringLocalizer<SharedResource>`.
- Keep `SharedResource` in the `Blogify.Web` namespace; moving it breaks resource lookup.
- Reuse a key only when its meaning is the same. Prefer short, feature-oriented dot-separated names.
- Do not branch UI text on the current culture.
