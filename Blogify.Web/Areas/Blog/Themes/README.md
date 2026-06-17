# Blog Theme Checklist

Blogify themes are first-party templates shipped with the application. Blog owners can choose from enabled registry entries, but they cannot upload arbitrary themes or customize colors, fonts, layouts, or CSS in this roadmap slice.

## Adding a Theme

1. Create a Razor folder under `Blogify.Web/Areas/Blog/Themes/{ThemeFolder}`.
2. Include `Shared/_Layout.cshtml` and all public partials required for parity:
   home, post, category, tag, search, archive, comments, pagination, sidebar, branding, SEO metadata, localization, mobile layout, keyboard focus, and reduced motion.
3. Add a source CSS file under `Blogify.Web/styles/themes/{slug}.css`.
4. Add or update a build script in `Blogify.Web/package.json` and include the source in `Blogify.Web/tailwind.config.js` content paths when needed.
5. Commit the compiled asset at `Blogify.Web/wwwroot/css/themes/{slug}.css`.
6. Add a preview image at `Blogify.Web/wwwroot/images/theme-previews/{slug}.png`.
7. Add a `BlogTheme` entry in `ThemeRegistry` with resource keys, preview path, Razor folder, CSS asset path, and selectable/enabled flags.
8. Add English and Turkish `.resx` entries for the theme name and description resource keys.
9. Run unit, localization, CSS build, and public-route smoke tests for every selectable theme.
10. Before enabling the theme, run a UX audit on desktop and mobile: no console warnings/errors, no 5xx or 404 asset failures, visible focus, readable contrast, no layout collapse, and clear first-time-reader behavior.

## Removing or Disabling a Theme

1. Mark the registry entry as not selectable before deleting files if old tenants may still reference the slug.
2. Confirm `ThemeRegistry.ResolveTheme` falls back to the registered fallback theme for old tenant data.
3. Verify public pages render with the fallback theme and no request fails because the removed theme is missing.
4. Verify the admin Appearance page warns that the saved theme is unavailable and allows choosing a valid selectable theme.
5. Remove Razor, CSS, preview image, localization keys, and build references only after old data has either been migrated or the registry entry is intentionally retained as disabled.

Only the registry fallback marker is special. Theme slugs such as `default`, `minimal`, and `aurora` are replaceable registry entries, not product concepts.
