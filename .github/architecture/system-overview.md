# System Overview — Blogify

## What This System Is

Blogify is a SaaS multi-tenant blogging platform. Each tenant is a blog, accessed via subdomain. The platform serves three distinct audiences from a single deployable: SuperAdmins managing the platform, BlogAdmins managing their own blog, and public readers viewing blog content.

---

## Project Responsibilities

### `Blogify.AppHost`
- Entry point for local orchestration via .NET Aspire.
- Declares all infrastructure resources: PostgreSQL, pgAdmin, Traefik.
- Wires `Blogify.Web` to the `blogdb` database resource.
- Starts Traefik container to route subdomain traffic to the web process.
- No application logic lives here. This project exists purely for dev-time composition.

### `Blogify.Web`
- The single deployable Razor Pages application.
- Hosts all three panels as isolated Areas:
  - `Areas/SuperAdmin/` — platform management
  - `Areas/BlogAdmin/` — per-tenant blog management
  - `Areas/Blog/` — public-facing blog rendering
- Owns the domain model, EF Core context, identity configuration, middleware, and application services.
- Registers and runs EF migrations on startup.
- Implements a **Themes system**: each blog has an `ActiveTheme` (`default`, `minimal`, `aurora`). A `ThemeViewLocationExpander` resolves Blog area views from `Areas/Blog/Themes/{Theme}/` before falling back to the standard view locations. BlogAdmins can switch themes via `Areas/BlogAdmin/Pages/Themes/`.

### `Blogify.ServiceDefaults`
- Shared cross-cutting configuration applied via `builder.AddServiceDefaults()`.
- Configures: OpenTelemetry (traces, metrics, logs), health endpoints, HTTP resilience defaults, service discovery.
- No feature-specific logic. Any addition here applies to every service using this package.

---

## Multi-Tenancy Architecture

- **Tenant = Blog**. One blog per subdomain.
- Subdomain is the sole tenant discriminator. Route-based tenancy is not used.
- Tenant resolution is performed by `TenantResolutionMiddleware` on every request.
- `localhost` and `saasplatform.local` are treated as platform hosts (no tenant context required).
- Unknown subdomains return `404 Blog not found.` — never fall through to application logic.

### Tenant Resolution Rules

| Host | Behavior |
|---|---|
| `localhost` | Platform dashboard. No tenant. SuperAdmin access. |
| `saasplatform.local` | Same as localhost. Platform root. |
| `{slug}.localhost` | Resolve tenant by slug. Set `TenantContext`. |
| Unknown slug | Return HTTP 404 immediately. |

---

## Aspire Architecture Role

```
[Developer Machine]
        │
        ▼
  Blogify.AppHost  (Aspire Orchestrator)
        │
        ├── PostgreSQL container  ──► blogdb
        ├── pgAdmin container     ──► admin UI
        ├── Traefik container     ──► subdomain routing
        └── Blogify.Web process   ──► port 5050
                │
                └── connects to blogdb via Aspire service discovery
```

- Aspire manages service lifetimes, connection strings, and OTel wiring automatically in development.
- In production, replace Aspire orchestration with environment variables and managed infrastructure.

---

## Request Flow

```
HTTP Request
    │
    ▼
TenantResolutionMiddleware
    │  ── reads Host header
    │  ── queries Blogs table by subdomain
    │  ── sets scoped TenantContext
    │
    ▼
AuthenticationMiddleware
    │  ── validates cookie/bearer token
    │  ── populates ClaimsPrincipal
    │
    ▼
AuthorizationMiddleware
    │  ── checks [Authorize] policies/roles
    │  ── rejects unauthorized requests
    │
    ▼
Razor Pages Router
    │  ── routes to Area by URL prefix
    │  ── /sa → SuperAdmin, /admin → BlogAdmin, else → Blog
    │
    ▼
PageModel.OnGetAsync / OnPostAsync
    │  ── injects ApplicationDbContext + TenantContext directly
    │  ── loads aggregate via EF Core query (filtered by BlogId)
    │  ── calls domain method on aggregate
    │  ── calls SaveChangesAsync (Unit of Work)
    │
    ▼
Domain Aggregate
    │  ── enforces invariants
    │  ── raises domain events
    │
    ▼
EF Core + PostgreSQL
```

---

## Health Endpoints

| Endpoint | Availability | Purpose |
|---|---|---|
| `/health` | Development only | Liveness + readiness combined |
| `/alive` | Development only | Liveness only (fast probe) |

---

## Traefik Routing

- Traefik is configured via `Blogify.AppHost/traefik.yml`.
- Routes `*.localhost` and `localhost` → `http://host.docker.internal:5050`.
- TLS is not configured for local development.

