# Blogify Agent Guide

Read this first. The code is the source of truth when documentation disagrees, so inspect nearby implementations before changing behavior.

## Stack

- .NET 10, ASP.NET Core Razor Pages, ASP.NET Identity, EF Core SQLite.
- .NET Aspire AppHost for local orchestration with Traefik subdomain routing.
- Server-rendered UI with Bootstrap, HTMX, vanilla JS, Tailwind-built public themes, and React only for the existing BlockNote editor.
- xUnit tests live in `Blogify.Tests`.
- Docker production image builds Tailwind and Vite assets during publish.

## Commands

- Restore solution: `dotnet restore Blogify.sln`
- Build app: `dotnet build Blogify.Web/Blogify.Web.csproj --no-restore`
- Run tests: `dotnet test Blogify.Tests/Blogify.Tests.csproj --no-restore`
- Run locally with Aspire: `dotnet run --project Blogify.AppHost`
- Build frontend assets after source changes:
  - `cd Blogify.Web && npm ci`
  - `npm run build:editor`
  - `npm run build:css:default`
  - `npm run build:css:minimal`
  - `npm run build:css:aurora`

Use the smallest relevant verification. Backend/domain changes normally need the xUnit project. Razor/CSS/JS changes need the .NET build and the relevant npm build script.

## Repository Map

- `Blogify.Web/`: deployable web app, Razor Pages, domain models, EF Core, middleware, endpoints, services, and static assets.
- `Blogify.Tests/`: xUnit coverage for tenancy, permissions, localization, storage, themes, caching, content organization, and moderation behavior.
- `Blogify.AppHost/`: Aspire composition for local development; runs `Blogify.Web` and Traefik.
- `Blogify.ServiceDefaults/`: shared telemetry, health checks, resilience, and service discovery.
- `Blogify.Web/Areas/Blog/`: public blog pages and theme views.
- `Blogify.Web/Areas/BlogAdmin/`: per-blog admin under `/app/admin/{blogSlug}`.
- `Blogify.Web/Areas/SuperAdmin/`: platform admin under `/sa`.
- `Blogify.Web/Pages/`: root-domain landing, dashboard, identity-adjacent pages, invitation flow, and shared layouts.
- `Blogify.Web/Models/`: domain entities. `Models/Posts/` contains the post aggregate.
- `Blogify.Web/Data/Configurations/`: EF Fluent mappings; generated migrations are in `Data/Migrations/`.
- `Blogify.Web/ClientApp/`: BlockNote React editor source. Generated bundles are under `wwwroot/`.
- `Blogify.Web/styles/themes/`: Tailwind inputs for public themes.

For tenancy, routing, authorization, themes, or data-boundary changes, also read `.github/architecture/system-overview.md`.

## Critical Rules

### Tenancy and Access

- A tenant is a blog. Public tenant resolution uses the host subdomain. BlogAdmin resolution uses `{blogSlug}` in `/app/admin/{blogSlug}`.
- Always use `TenantContext` and `ApplicationDbContext.CurrentTenantId` as established by middleware. Never trust a route value, claim, hidden field, or submitted tenant ID alone.
- Global tenant/soft-delete query filters exist for `Post`, `Category`, `Tag`, `Media`, and `Comment`.
- `BlogMembership`, `BlogInvitation`, and `AnalyticsEvent` do not have tenant global filters. Scope them explicitly.
- Ownership is `Tenant.OwnerId`. Delegated access is `BlogMembership` with `BlogRole` (`Writer`, `Editor`, `Admin`). There is no `ApplicationUser.TenantId`.
- Use `IBlogPermissionService` for role-gated actions and keep area access consistent with `AccessControlMiddleware`.
- Keep middleware order in `Program.cs`: forwarded headers, HTTPS/static files, routing, authentication, tenant resolution, public-blog culture, access control, analytics, authorization, output cache, antiforgery, endpoints.

### Backend

- Put business invariants and state transitions in domain methods; do not mutate entity state directly from PageModels when a domain method exists.
- PageModels may use `ApplicationDbContext` directly for request orchestration. Add focused services only for shared behavior or infrastructure such as permissions, storage, feeds, analytics, rendering, and email.
- Use async EF APIs and `AsNoTracking()` for read-only queries. Do not expose `IQueryable` outside the method that builds it.
- For a normal mutation, load data, call domain methods, and call `SaveChangesAsync` once.
- Use EF Fluent configuration for domain entities. Input-model validation attributes are allowed.
- Use `DateTimeOffset` for persisted dates and existing soft-delete methods instead of physical deletion.
- Minimal endpoint groups belong in `Blogify.Web/Endpoints/` with `Map{Feature}Endpoints` extensions registered in `Program.cs`.
- Do not hand-edit generated migrations or the model snapshot unless the EF tooling cannot express the required change.

### Razor, Frontend, and Localization

- Prefer server-rendered Razor Pages and existing area layouts/partials.
- Use strongly typed PageModel properties for page data. `ViewData` is allowed for page titles and the established public-theme SEO metadata contract.
- Localize user-visible Razor text through `SharedResource`; keep English and Turkish resource files in sync.
- Use Tag Helpers for forms, links, validation, and partials. Preserve antiforgery protection.
- React is limited to the BlockNote editor under `ClientApp/`. Use HTMX or small vanilla JS elsewhere unless explicitly asked to change the frontend architecture.
- Edit source files in `ClientApp/` and `styles/themes/`; do not manually edit generated `wwwroot` bundles when source exists.
- Follow `PRODUCT.md` and `DESIGN.md` for UI tone, layout, colors, and accessibility expectations.

## Change Discipline

- Keep changes scoped to the requested behavior. Avoid new layers, dependencies, or rewrites unless the surrounding code already points there.
- Preserve user changes and generated-file conventions.
- When behavior is unclear, inspect the model, EF configuration, middleware, PageModel, service, and tests before deciding.
- Update these instructions only for stable, cross-cutting facts. Avoid inventories of pages, properties, or methods that are easy to rediscover with `rg`.
