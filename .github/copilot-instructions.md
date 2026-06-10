# Blogify Agent Guide

Read this file first. Inspect nearby code before changing behavior; the code is the source of truth when documentation disagrees.

## Stack and Commands

- .NET 10, ASP.NET Core Razor Pages, EF Core with SQLite, .NET Aspire.
- Server-rendered UI with Bootstrap, HTMX, small vanilla JS, and React only for the existing BlockNote editor.
- Restore/build: `dotnet restore Blogify.Web/Blogify.Web.csproj` then `dotnet build Blogify.Web/Blogify.Web.csproj --no-restore`.
- Run locally: `dotnet run --project Blogify.AppHost`.
- Build editor/themes after related frontend changes:
  `cd Blogify.Web && npm ci && npm run build:editor && npm run build:css:minimal && npm run build:css:aurora`.
- There is currently no automated test project. At minimum, run the relevant build.

## Repository Map

- `Blogify.Web/`: application, domain models, EF Core, Razor Pages, middleware, endpoints, and frontend assets.
- `Blogify.AppHost/`: local Aspire orchestration and Traefik.
- `Blogify.ServiceDefaults/`: shared telemetry, health checks, resilience, and service discovery only.
- `Blogify.Web/Areas/Blog/`: public blog pages and theme views.
- `Blogify.Web/Areas/BlogAdmin/`: tenant admin pages.
- `Blogify.Web/Areas/SuperAdmin/`: platform admin pages.
- `Blogify.Web/Pages/`: root-domain landing, dashboard, setup, and shared pages.
- `Blogify.Web/Models/`: domain entities. `Models/Posts/` contains the Post aggregate.
- `Blogify.Web/Data/Configurations/`: EF mappings; migrations are in `Data/Migrations/`.
- `Blogify.Web/ClientApp/`: BlockNote React editor source. Generated bundles are under `wwwroot/`.

See [system-overview.md](architecture/system-overview.md) only when working on routing, tenancy, authorization, themes, or data boundaries.

## Critical Rules

### Tenancy and Access

- A tenant is a blog. Public tenant resolution uses the subdomain; BlogAdmin resolution uses the `{blogSlug}` route in `/app/admin/{blogSlug}`.
- Always take the current tenant from `TenantContext`, never trust a route value, claim, or submitted tenant ID by itself.
- `Post`, `Category`, `Media`, and `Comment` have tenant/soft-delete global query filters. Do not add redundant tenant filters to normal scoped queries.
- `Tag`, `BlogMembership`, `BlogInvitation`, and `AnalyticsEvent` do not have tenant global filters. Scope their queries explicitly where relevant.
- Ownership is `Tenant.OwnerId`. Multi-blog access is `BlogMembership` with `BlogRole` (`Writer`, `Editor`, `Admin`). Do not use an `ApplicationUser.TenantId`; it does not exist.
- Keep middleware order in `Program.cs`: routing, authentication, tenant resolution, access control, analytics, authorization, antiforgery.

### Backend

- Put business invariants and state changes in domain methods; do not mutate entity properties from handlers.
- PageModels orchestrate requests and may use `ApplicationDbContext` directly. Keep focused services for behavior shared across pages or involving infrastructure, such as permissions, storage, feeds, analytics, and rendering.
- Use async EF APIs and `AsNoTracking()` for read-only queries. Materialize queries before returning them.
- For a normal mutation, load data, call domain methods, and call `SaveChangesAsync` once.
- Use EF Fluent configuration rather than data annotations on domain entities. Input-model validation attributes are allowed.
- Use `DateTimeOffset` for persisted dates and soft-delete entities that already support `DeletedAt`.
- Minimal endpoints belong in `Blogify.Web/Endpoints/` and are mapped from `Program.cs`.
- Do not hand-edit generated migrations or the model snapshot unless migration tooling cannot perform the required change.

### Razor and Frontend

- Prefer server-rendered Razor Pages and existing local patterns.
- Use strongly typed PageModel properties for page data. `ViewData` is allowed for conventional page titles and the existing theme SEO metadata contract.
- Localize user-visible Razor text and keep English/Turkish resource keys in sync.
- React is limited to the existing BlockNote editor. Use small vanilla JS or HTMX elsewhere unless a broader frontend change is explicitly requested.
- Do not edit generated frontend bundles when source files exist.

## Change Discipline

- Keep changes scoped; do not introduce a new architectural layer or dependency without a concrete need.
- Preserve existing user changes and generated-file conventions.
- When behavior is unclear, inspect the model, EF configuration, middleware, and calling PageModel before deciding.
- Update this documentation only for stable, cross-cutting facts. Avoid inventories of properties, pages, or methods that are easy to discover with `rg`.
