---
applyTo: "**/*.cs"
---

# Backend Rules

- Follow nearby code and the repository-wide guide before introducing a new pattern.
- Domain entities control their state through named methods. Keep setters non-public and validate invariants at creation or mutation.
- PageModels are request orchestrators: query, call domain behavior, save, return. Focused services are valid for shared permissions, storage, feeds, analytics, rendering, email, and other infrastructure behavior.
- Use async EF Core APIs. Add `AsNoTracking()` to read-only queries and do not expose `IQueryable`.
- Normal mutations call `SaveChangesAsync` once after all state changes.
- Use `TenantContext.RequiredTenant`, `TenantContext.CurrentTenantId`, or `ApplicationDbContext.CurrentTenantId` set by middleware; never authorize or scope data from a submitted tenant ID alone.
- Global tenant/soft-delete filters exist for `Post`, `Category`, `Tag`, `Media`, and `Comment`.
- Explicitly scope entities without those filters, especially `BlogMembership`, `BlogInvitation`, and `AnalyticsEvent`.
- Ownership is `Tenant.OwnerId`; delegated access is `BlogMembership` plus `BlogRole`. Use `IBlogPermissionService` for role-gated actions.
- Use `DateTimeOffset` for persisted dates. Use existing soft-delete methods instead of physical deletion.
- Keep EF mappings in `Data/Configurations/` and use Fluent API for domain entities.
- Put minimal API groups in `Endpoints/` with a `Map{Feature}Endpoints` extension and register them in `Program.cs`.
- Prefer primary-constructor injection where the surrounding code uses it. Use `sealed` for new classes not intended for inheritance.
- Do not add placeholder code, suppress nullable warnings without cause, or hand-edit generated migrations by default.
