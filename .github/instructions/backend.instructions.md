---
applyTo: "**/*.cs"
---

# Backend Instructions — Blogify (.NET / C#)

## Aggregates & Invariants

- Every aggregate root inherits from a base class or implements a marker interface that exposes domain events.
- Aggregate constructors validate all invariants before setting any state. Throw `DomainException` (custom) on violation.
- Factory methods (`Create(...)`) are preferred over public constructors for complex aggregates.
- Required fields in aggregates use `private readonly` backing fields where mutation must be controlled.
- Collections inside aggregates are `private readonly List<T>` exposed as `IReadOnlyList<T>`. Never expose the mutable list.

Example aggregate structure:
```csharp
public sealed class Post
{
    private readonly List<PostRevision> _revisions = [];

    private Post() { } // EF constructor

    private Post(Guid blogId, string slug, PostRevision initialRevision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(initialRevision);

        Id = Guid.NewGuid();
        BlogId = blogId;
        Slug = slug;
        _revisions.Add(initialRevision);
        Status = PostStatus.Draft;
    }

    public static Post Create(Guid blogId, string slug, string initialTitle, string initialContent)
    {
        PostRevision revision = PostRevision.Create(initialTitle, initialContent);
        return new Post(blogId, slug, revision);
    }

    public Guid Id { get; private init; }
    public Guid BlogId { get; private init; }
    public string Slug { get; private set; }
    public PostStatus Status { get; private set; }
    public Guid? PublishedRevisionId { get; private set; }
    public IReadOnlyList<PostRevision> Revisions => _revisions.AsReadOnly();

    public PostRevision AddRevision(string title, string content)
    {
        PostRevision revision = PostRevision.Create(title, content);
        _revisions.Add(revision);
        return revision;
    }

    public void Publish(Guid revisionId)
    {
        if (!_revisions.Any(r => r.Id == revisionId))
            throw new DomainException("Revision does not belong to this post.");
        PublishedRevisionId = revisionId;
        Status = PostStatus.Published;
    }

    public void Unpublish()
    {
        PublishedRevisionId = null;
        Status = PostStatus.Draft;
    }
}
```

---

## Entity Rules

- Every entity has a `Guid Id` with `private init` access.
- No entity has a public parameterless constructor except for EF Core (must be `private`).
- Dates use `DateTimeOffset` (never `DateTime`).
- String properties have max-length enforced in EF configuration, not via data annotations.
- Soft delete is expressed via `DeletedAt` nullable field. Never physically delete domain entities.

---

## Value Objects

- Implemented as `readonly record struct` for small values, `sealed record` for complex ones.
- Validation inside the constructor. Throw `ArgumentException` on invalid input.
- No value object has an `Id`. If it needs an `Id`, it is an entity.

---

## Data Access — DbContext Direct Usage

- There are no repository interfaces or implementations. PageModels inject `ApplicationDbContext` directly.
- All queries are written as async LINQ against `dbContext.Set<T>()` or named `DbSet<T>` properties.
- Never return `IQueryable<T>` from any method. Always materialize with `ToListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, etc.
- Use `AsNoTracking()` on all read-only queries that do not result in tracked mutations.
- Call `SaveChangesAsync` exactly once at the end of a mutating handler. Never call it more than once per operation.
- Every query on tenant-scoped data (Post, Category, Tag, Media) must include a `.Where(x => x.BlogId == blogId)` clause.
- `blogId` is always obtained from the scoped `TenantContext`. Never derive it from route parameters or claims alone.

Example PageModel handler (read + mutate):
```csharp
public sealed class PublishModel(ApplicationDbContext dbContext, TenantContext tenantContext) : PageModel
{
    public async Task<IActionResult> OnPostAsync(Guid id, Guid revisionId, CancellationToken ct)
    {
        Guid blogId = tenantContext.RequiredBlogId;
        Post? post = await dbContext.Posts
            .Include(p => p.Revisions)
            .Where(p => p.BlogId == blogId && p.Id == id)
            .FirstOrDefaultAsync(ct);

        if (post is null) return NotFound();

        post.Publish(revisionId);
        await dbContext.SaveChangesAsync(ct);
        return RedirectToPage("./Index");
    }
}
```

---

## Orchestration Pattern (PageModel)

- PageModel `OnGetAsync` / `OnPostAsync` are the orchestration layer: load aggregate via `ApplicationDbContext` → call domain method → call `SaveChangesAsync` → return result.
- No separate application service classes exist. All orchestration belongs in the PageModel handler.
- Authorization checks happen via `[Authorize]` attributes or `IAuthorizationService`, not inside the handler logic.
- Do not put business rules directly in `OnGetAsync`/`OnPostAsync` — business rules belong in the domain aggregate.

---

## EF Core Configuration

- All entity configurations implement `IEntityTypeConfiguration<T>` and are discovered via `ApplyConfigurationsFromAssembly`.
- No data annotations on domain entities.
- Use `HasConversion` for value objects and enums stored as strings.
- Owned entities use `OwnsOne` / `OwnsMany`.
- All foreign keys are explicit. Never rely on EF shadow properties for business-critical FKs.

---

## Tenant Isolation

- Every query against tenant-scoped data (Post, Category, Tag, Media) MUST filter by `BlogId`.
- PageModels receive `Guid blogId` from the resolved `TenantContext`. Never derive `blogId` from user claims alone.
- Aggregate methods that modify tenant data verify `BlogId` matches before mutating state.

---

## Exception Handling

- `DomainException : Exception` — invariant violations from aggregates.
- `NotFoundException : Exception` — aggregate not found by ID.
- `TenantAccessException : Exception` — cross-tenant access attempt.
- Map these to HTTP status codes in a global exception handler middleware. Never leak exception details in production.

---

## Naming Conventions

| Concept | Convention |
|---|---|
| Aggregate root | `Post`, `Blog`, `Category` |
| Value object | `Slug`, `MediaUrl` |
| Domain event | `PostPublishedEvent` |
| EF configuration | `PostEntityConfiguration` |
| PageModel | `IndexModel`, `EditModel`, `PublishModel` |
| View model record | `PostSummary`, `PostDetail` |
| Input model record | `CreatePostInput`, `PublishPostInput` |
