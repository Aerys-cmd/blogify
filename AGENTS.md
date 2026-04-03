# AGENTS.md

## Project Snapshot
- Solution: `Blogify.sln` with 3 projects: `Blogify.AppHost`, `Blogify.Web`, `Blogify.ServiceDefaults`.
- Runtime target is `.NET 10` preview (`global.json` pins `10.0.0`, prerelease enabled).
- Current app is a multi-tenant Razor Pages app with ASP.NET Core Identity + PostgreSQL.

## Architecture That Matters
- `Blogify.AppHost/AppHost.cs` is the composition root for local dev orchestration (Aspire):
  - starts PostgreSQL + pgAdmin (`AddPostgres(...).WithPgAdmin()`),
  - creates DB resource `blogdb`,
  - runs `Blogify.Web` at fixed HTTP port `5050`,
  - starts Traefik container using `Blogify.AppHost/traefik.yml`.
- `Blogify.ServiceDefaults/Extensions.cs` centralizes cross-cutting defaults:
  - OpenTelemetry, resilience, service discovery, health endpoints (`/health`, `/alive` only in Development).
- `Blogify.Web/Program.cs` wires app behavior:
  - `builder.AddServiceDefaults()` and `builder.AddNpgsqlDbContext<ApplicationDbContext>("blogdb")`,
  - Identity EF stores,
  - auto migration at startup: `Database.MigrateAsync()`.

## Tenant Routing/Data Flow
- Tenant resolution is host-based, not route-based.
- Pipeline: request host -> `Blogify.Web/Middleware/TenantResolutionMiddleware.cs` -> query `ApplicationDbContext.Blogs` -> set scoped `TenantContext.CurrentTenant`.
- `localhost` and `saasplatform.local` are treated as dashboard/root (no tenant required).
- Unknown subdomain returns HTTP 404 with plain text `Blog not found.`.
- UI consumes scoped tenant state directly in Razor via DI (`Blogify.Web/Pages/Index.cshtml` uses `@inject TenantContext`).

## Data Model Conventions
- `ApplicationDbContext` is an `IdentityDbContext<ApplicationUser>` and applies all entity configs via assembly scan (`ApplyConfigurationsFromAssembly`).
- Tenant entity is named `Tenant` in code but mapped as `Blogs` table/DbSet (`ApplicationDbContext.Blogs`).
- Entity constraints are fluent-only in `Blogify.Web/Data/Configurations/TenantEntityConfiguration.cs` (e.g., unique `Subdomain`, max lengths).
- Existing migration baseline is in `Blogify.Web/Data/Migrations/20260319222648_AddTenantEntity.cs`.

## Developer Workflows (Observed)
- Build (verified): `dotnet build Blogify.sln`.
- Preferred run path is via Aspire orchestrator: `dotnet run --project Blogify.AppHost`.
- If adding/changing EF entities in `Blogify.Web`, generate migrations from that project and keep snapshot in sync.

## Integration/Infra Notes
- Traefik rule in `Blogify.AppHost/traefik.yml` routes `*.localhost` and `localhost` to web app via `http://host.docker.internal:5050`.
- `Blogify.Web/appsettings.Development.json` includes direct local Postgres fallback connection string (`blogdb`).
- OTel exporter is opt-in via `OTEL_EXPORTER_OTLP_ENDPOINT` (see `Blogify.ServiceDefaults/Extensions.cs`).

## Agent Guardrails for Changes
- Preserve middleware order in `Program.cs` (`UseAuthentication`/`UseAuthorization` before tenant middleware).
- Keep resource name `blogdb` consistent across AppHost and Web registration.
- When changing tenant behavior, update both middleware logic and tenant-aware Razor usage.
- There are currently no test projects in repo; validate changes at minimum with solution build + startup smoke run.
