---
applyTo: "**/*.cshtml,**/*.cshtml.cs,**/*.js,**/*.ts,**/*.tsx,**/*.css"
---

# Frontend Rules

- Prefer server-rendered Razor Pages and reuse the nearest area's layout, partials, and styling patterns.
- Keep request/data logic in PageModels. Use strongly typed properties for page data.
- `ViewData` is allowed for `Title` and the existing public-theme SEO values: `MetaTitle`, `MetaDescription`, `OgImage`, `OgType`, and `CanonicalUrl`.
- Use Tag Helpers for forms, links, validation, and partials. Preserve antiforgery protection.
- Localize user-visible Razor text through `SharedResource`; update both English and Turkish resources together.
- Follow `PRODUCT.md` and `DESIGN.md` for product tone, color, layout, and accessibility. Do not regress to generic Bootstrap-admin or gradient-SaaS patterns.
- Use Bootstrap and existing CSS conventions for product/admin pages. Public theme overrides live under `Areas/Blog/Themes/` and Tailwind inputs under `styles/themes/`.
- Use HTMX or small vanilla JS for progressive enhancement. Avoid inline event handlers.
- React is limited to the existing BlockNote editor under `ClientApp/`. Do not introduce React elsewhere without explicit approval.
- Edit editor source in `ClientApp/` and theme source in `styles/themes/`; do not manually edit generated `wwwroot` outputs.
- Keep markup accessible: labels for inputs, meaningful button text or accessible labels, useful image alt text, semantic elements, visible focus, and resilient English/Turkish layouts.
