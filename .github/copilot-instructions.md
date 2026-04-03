# Copilot Instructions — Blogify

## Project Identity
- SaaS multi-tenant blog platform
- Runtime: .NET 10, ASP.NET Core, Razor Pages, PostgreSQL, Aspire
- Architecture: Modular Monolith + Domain-Driven Design (DDD)
- Frontend: Bootstrap 5, server-rendered, optional HTMX

---

## DDD Rules (Strict)

- Every domain concept lives in a named aggregate. No free-floating entities.
- Aggregates expose behavior through methods. Properties are never mutated from outside.
- Invariants are enforced inside the aggregate constructor or domain methods — never in application or infrastructure layers.
- Use `private set` or `init` on all entity properties. No public setters.
- Domain events are raised inside aggregates when state changes. Never from handlers.
- Value objects are immutable records. If it has identity, it is an entity. If not, it is a value object.
- No `static` utility classes that encode domain logic.
- Application services inject `ApplicationDbContext` directly. No repository interfaces or implementations.
- Application services (handlers) orchestrate only. No business logic inside them.
- Do not use EF Core navigation properties to bypass aggregate boundaries.

---

## Razor Pages Structure

- Each panel is isolated under its own `Area`:
  - `Areas/SuperAdmin/` — route prefix `/sa`
  - `Areas/BlogAdmin/` — route prefix `/admin`
  - `Areas/Blog/` — public blog, subdomain-resolved
- PageModel classes use primary constructor injection.
- `OnGetAsync` / `OnPostAsync` do not contain business logic. They delegate to application services.
- View models are `sealed record` types defined at the bottom of the PageModel file.
- Never use `ViewData` or `ViewBag`. Use strongly-typed properties on PageModel.
- Shared layout and partials live in `Pages/Shared/`. Area-specific ones in `Areas/{Area}/Pages/Shared/`.
- Tag Helpers and Partial Tag Helpers are preferred over HTML Helpers.

---

## Aspire Usage

- All service wiring happens in `Blogify.AppHost/AppHost.cs`. Never hard-code connection strings outside `appsettings.Development.json`.
- Resource names must be stable constants. `"blogdb"` is the canonical DB resource name.
- `Blogify.ServiceDefaults/Extensions.cs` is the only place for cross-cutting defaults (OTel, health, resilience).
- Do not add middleware or configuration in `ServiceDefaults` that is feature-specific.
- Health check endpoints: `/health` (liveness + readiness), `/alive` (liveness only, Development only).

---

## Identity Usage

- Roles: `SuperAdmin`, `BlogAdmin`. No other roles are defined.
- Authorization is enforced at two layers: endpoint (`[Authorize(Roles = ...)]`) and domain (aggregate checks tenant ownership).
- Each `BlogAdmin` user is associated with exactly one `Tenant` (Blog). This association is stored on `ApplicationUser`.
- Never trust role alone for data access. Always verify tenant ownership in the domain layer.
- Authentication pages live under `Areas/Identity/` (scaffolded or custom). Do not move them.

---

## Code Quality Rules

- No `var` where type is not obvious from the right-hand side.
- No nullable reference warnings suppressed with `!`. Fix the root cause.
- All async methods end with `Async`. All return `Task` or `ValueTask`.
- Use `sealed` on all classes that are not designed for inheritance.
- Use `IReadOnlyList<T>` or `IReadOnlyCollection<T>` for exposing collections from domain objects.
- Use `required` keyword on DTO/record properties that must always be provided.
- Use `ArgumentNullException.ThrowIfNull` at aggregate/service boundaries.
- No commented-out code in committed files.
- No `TODO` or `FIXME` left in generated code.

---

## Output Expectations

- Every generated file must be complete and immediately compilable.
- Never produce partial code with placeholder comments like `// ... rest of implementation`.
- When generating a PageModel, always include the full `OnGetAsync`/`OnPostAsync` and all view model records.
- When generating an entity, include all properties, constructors, domain methods, and EF configuration class.
- When generating a migration, verify the snapshot is in sync.
- When generating a service, include the interface, implementation (injecting `ApplicationDbContext`), and DI registration.

