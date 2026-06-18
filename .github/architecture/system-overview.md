# System Overview

Use this reference for changes involving tenancy, routing, authorization, themes, storage, caching, localization, or data boundaries. For ordinary feature work, inspect the nearest implementation instead.

## Runtime Shape

`Blogify.Web` is the single deployable ASP.NET Core Razor Pages application. It uses SQLite, ASP.NET Identity, EF Core migrations, localized resources, output caching for public pages, background email dispatch, and background analytics persistence.

`Blogify.AppHost` runs the web project and Traefik for local subdomain routing. `Blogify.ServiceDefaults` contains only shared observability, health checks, resilience, and service-discovery setup.

The app starts by running EF migrations and database seeding. Default service endpoints are mapped through `MapDefaultEndpoints()`.

## Routes and Tenant Resolution

| Surface | Route/host | Tenant source |
|---|---|---|
| Landing, dashboard, identity, invitation | platform host; friendly routes such as `/dashboard`, `/login`, `/register`, `/invite/{token}` | none |
| SuperAdmin | `/sa` and lowercase child routes on platform host | none |
| BlogAdmin | `/app/admin/{blogSlug}` and lowercase child routes on platform host | route slug resolved by middleware |
| Public blog | tenant subdomain | host subdomain |

Platform hosts come from `TenantOptions.PlatformHosts`. `TenantResolutionMiddleware` resolves a `Tenant`, sets `TenantContext`, and sets `ApplicationDbContext.CurrentTenantId`. Unknown tenant slugs return 404.

Razor Pages route conventions in `Program.cs` replace broad area paths with friendly lowercase public URLs; old area paths are not compatibility routes. `AccessControlMiddleware` enforces host/area boundaries and requires ownership or membership for BlogAdmin. `IBlogPermissionService` handles action-level role checks.

Middleware order is significant:

```text
ForwardedHeaders -> HttpsRedirection -> StaticFiles -> Routing -> Authentication
-> TenantResolution -> PublicBlogCulture -> AccessControl -> AnalyticsTracking
-> Authorization -> OutputCache -> Antiforgery -> Endpoints
```

## Access Model

- A user can own blogs through `Tenant.OwnerId`.
- Non-owner access is represented by `BlogMembership`.
- Membership roles are `Writer`, `Editor`, and `Admin`.
- Invitations become memberships through `/invite/{token}`.
- ASP.NET Identity roles are separate from per-blog membership roles. `SuperAdmin` does not automatically grant access to a blog.

Never authorize tenant data using only a route value, claim, or submitted ID. Resolve the tenant and verify ownership, membership, or the required permission.

## Data Boundaries

`ApplicationDbContext` applies tenant and soft-delete global filters to:

- `Post`
- `Category`
- `Tag`
- `Media`
- `Comment`

These filters use `CurrentTenantId`. When it is null, they only apply soft-delete filtering, so platform-level code must still scope queries appropriately.

The following do not have tenant global filters and require explicit scoping where applicable:

- `BlogMembership`
- `BlogInvitation`
- `AnalyticsEvent`

`Tenant` is exposed as `ApplicationDbContext.Blogs`. Ownership and membership are separate records. Post revisions, post-category links, and post-tag links are managed through the `Post` aggregate.

## Public Content and Themes

Public blogs use `Tenant.ActiveTheme`: `default`, `minimal`, or `aurora`. `ThemeViewLocationExpander` resolves views from `Areas/Blog/Themes/{Theme}/`.

The public theme layouts consume SEO values through the established `ViewData` contract:

- `MetaTitle`
- `MetaDescription`
- `OgImage`
- `OgType`
- `CanonicalUrl`

Posts store BlockNote JSON in `PostRevision.Content` and searchable/plain text in `ContentText`. The React BlockNote editor is isolated under `ClientApp/`; public rendering uses server-side rendering services. Rich embeds render as trusted provider iframes only for the renderer allowlist, and otherwise fall back to safe external links.

## Cross-Cutting Features

- Localization: English and Turkish shared resources in `Blogify.Web/Resources`.
- Public blog culture: owner-selected language is applied by `UsePublicBlogCulture`.
- Media: `IFileStorageService` uses local storage by default and Cloudflare R2 when all `Storage:R2` settings are present. Partial R2 configuration fails startup.
- Images: `ImageStorageProcessor` applies configured image and thumbnail quality settings.
- Feeds: tenant-scoped RSS and sitemap endpoints in `Blogify.Web/Endpoints/`.
- Analytics: middleware queues public page views; a hosted service persists them.
- Public caching: selected Blog area pages use `PublicBlogOutputCachePolicy`; use `IPublicBlogCacheInvalidator` when mutations should invalidate public output.
- Email: localized Razor emails are queued in memory and delivered by SMTP when `Email:Enabled` is true; disabled delivery logs and discards messages.
- Feedback Hub: Blog Admin renders the widget only when `FeedbackHub:PublicKey` is configured.
- Data protection keys persist to `keys/` in development and `/app/keys` in production unless overridden.
