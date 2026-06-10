# System Overview

Use this reference for changes involving tenancy, routing, authorization, themes, or data boundaries. For ordinary feature work, inspect the nearest implementation instead.

## Runtime Shape

`Blogify.Web` is the single deployable ASP.NET Core Razor Pages application. It uses SQLite, ASP.NET Identity, and local file storage. `Blogify.AppHost` runs the web project and Traefik for local subdomain routing. `Blogify.ServiceDefaults` contains only shared observability, health, resilience, and service-discovery setup.

The application starts by running EF migrations and database seeding. Development health endpoints are `/health` and `/alive`.

## Routes and Tenant Resolution

| Surface | Route/host | Tenant source |
|---|---|---|
| Landing, dashboard, setup, identity | platform host | none |
| SuperAdmin | `/sa` on platform host | none |
| BlogAdmin | `/app/admin/{blogSlug}` on platform host | route slug |
| Public blog | tenant subdomain | host subdomain |

Platform hosts come from `TenantOptions`. `TenantResolutionMiddleware` resolves a `Tenant`, sets `TenantContext`, and sets `ApplicationDbContext.CurrentTenantId`. Unknown tenant slugs return 404.

`AccessControlMiddleware` then enforces host/area boundaries and requires ownership or membership for BlogAdmin. `IBlogPermissionService` handles action-level role checks.

Middleware order is significant:

```text
Routing -> Authentication -> TenantResolution -> AccessControl
        -> AnalyticsTracking -> Authorization -> Antiforgery
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
- `Media`
- `Comment`

These filters use `CurrentTenantId`. When it is null, they only apply soft-delete filtering, so platform-level code must still scope queries appropriately.

The following do not have tenant global filters and require explicit scoping where applicable:

- `Tag`
- `BlogMembership`
- `BlogInvitation`
- `AnalyticsEvent`

`Tenant` is exposed as `ApplicationDbContext.Blogs`. Ownership and membership are separate records. Post revisions and post-category links are managed through the `Post` aggregate.

## Public Content and Themes

Public blogs use `Tenant.ActiveTheme`: `default`, `minimal`, or `aurora`. `ThemeViewLocationExpander` resolves views from `Areas/Blog/Themes/{Theme}/`.

The public theme layouts consume SEO values through the established `ViewData` contract:

- `MetaTitle`
- `MetaDescription`
- `OgImage`
- `OgType`
- `CanonicalUrl`

Posts store BlockNote JSON in `PostRevision.Content` and searchable/plain text in `ContentText`. The React BlockNote editor is isolated under `ClientApp/`; public rendering uses server-side rendering services.

## Cross-Cutting Features

- Localization: shared English and Turkish resources.
- Media: `IFileStorageService`, currently local storage.
- Feeds: tenant-scoped RSS and sitemap endpoints.
- Analytics: middleware queues public page views; a hosted service persists them.
- Data protection keys persist to `keys/` in development and `/app/keys` in production.
