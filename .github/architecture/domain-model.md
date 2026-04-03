# Domain Model — Blogify

## Aggregate Map

```
┌─────────────────────────────────────────────────────────────────┐
│  Tenant (Blog) Aggregate                                        │
│  Root: Tenant                                                   │
│  ─────────────────────────────────────────────────────────────  │
│  Tenant { Id, Title, Subdomain, OwnerId, CreatedAt, DeletedAt } │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Post Aggregate                                                 │
│  Root: Post                                                     │
│  ─────────────────────────────────────────────────────────────  │
│  Post { Id, BlogId, Slug, Status, PublishedRevisionId,          │
│         CreatedAt, DeletedAt }                                  │
│    └── PostRevision[] { Id, PostId, Title, Content, CreatedAt } │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Category Aggregate                                             │
│  Root: Category                                                 │
│  ─────────────────────────────────────────────────────────────  │
│  Category { Id, BlogId, Name, Slug, CreatedAt, DeletedAt }      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Tag Aggregate                                                  │
│  Root: Tag                                                      │
│  ─────────────────────────────────────────────────────────────  │
│  Tag { Id, BlogId, Name, Slug, CreatedAt, DeletedAt }           │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  Media Aggregate                                                │
│  Root: Media                                                    │
│  ─────────────────────────────────────────────────────────────  │
│  Media { Id, BlogId, FileName, Url, ContentType, SizeBytes,     │
│          UploadedAt, DeletedAt }                                │
└─────────────────────────────────────────────────────────────────┘
```

---

## Entities

### Tenant
Represents a blog and its subdomain identity. This is the top-level multi-tenancy boundary.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `Title` | `string` | Display name, max 200 chars |
| `Subdomain` | `string` | Unique across system, max 63 chars, lowercase |
| `OwnerId` | `string` | FK to `ApplicationUser.Id` |
| `CreatedAt` | `DateTimeOffset` | Set on creation, immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### Post
A piece of content published under a blog. Identity is the aggregate root.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `BlogId` | `Guid` | FK to `Tenant.Id`, `private init` |
| `Slug` | `string` | Unique per blog, URL-safe, max 300 chars |
| `Status` | `PostStatus` | Enum: `Draft`, `Published` |
| `PublishedRevisionId` | `Guid?` | Points to active revision when published |
| `CreatedAt` | `DateTimeOffset` | Immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### PostRevision
Immutable snapshot of a post's content at a point in time. Owned by Post aggregate.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `PostId` | `Guid` | FK to `Post.Id` |
| `Title` | `string` | max 500 chars |
| `Content` | `string` | Full HTML/Markdown body |
| `CreatedAt` | `DateTimeOffset` | Immutable, set on creation |

### Category
A taxonomy concept scoped to a single blog.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `BlogId` | `Guid` | FK to `Tenant.Id`, `private init` |
| `Name` | `string` | max 100 chars |
| `Slug` | `string` | Unique per blog, max 100 chars |
| `CreatedAt` | `DateTimeOffset` | Immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### Tag
A lightweight label scoped to a single blog.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `BlogId` | `Guid` | FK to `Tenant.Id`, `private init` |
| `Name` | `string` | max 100 chars |
| `Slug` | `string` | Unique per blog, max 100 chars |
| `CreatedAt` | `DateTimeOffset` | Immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### Media
An uploaded asset file scoped to a single blog.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `BlogId` | `Guid` | FK to `Tenant.Id`, `private init` |
| `FileName` | `string` | Original file name, max 255 chars |
| `Url` | `string` | Resolved public URL |
| `ContentType` | `string` | MIME type, max 100 chars |
| `SizeBytes` | `long` | File size in bytes |
| `UploadedAt` | `DateTimeOffset` | Immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### ApplicationUser
ASP.NET Identity user. Extended with blog association.

| Property | Type | Notes |
|---|---|---|
| `Id` | `string` | Identity PK |
| `BlogId` | `Guid?` | FK to `Tenant.Id`. `null` for SuperAdmin. |
| Standard Identity fields | — | Email, PasswordHash, etc. |

---

## Relationships

```
ApplicationUser ──── (one) ───► Tenant           (owner)
Tenant          ──── (many) ──► Post             (posts)
Tenant          ──── (many) ──► Category         (categories)
Tenant          ──── (many) ──► Tag              (tags)
Tenant          ──── (many) ──► Media            (media)
Post            ──── (many) ──► PostRevision     (revisions, owned)
Post            ──── (many-to-many) ──► Category
Post            ──── (many-to-many) ──► Tag
```

---

## Business Rules

### Tenant (Blog)
- `Subdomain` must be globally unique. Enforced via unique index.
- `Subdomain` must match `^[a-z0-9-]+$`. Validated in aggregate constructor.
- A tenant can only be owned by one user (one-to-one with `BlogAdmin` user).
- Soft-deleted tenants are hidden from subdomain resolution immediately.

### Post
- A post MUST have at least one `PostRevision` at all times.
- The first revision is created atomically with the post via `Post.Create(...)`.
- `Slug` must be unique per `BlogId`. Enforced via unique index on `(BlogId, Slug)`.
- `Slug` must match `^[a-z0-9-]+$`. Validated in aggregate constructor.
- A post can only be published by pointing `PublishedRevisionId` to one of its own revisions.
- Attempting to publish with a `revisionId` not belonging to the post throws `DomainException`.
- Unpublishing clears `PublishedRevisionId` and sets `Status = Draft`.
- Adding a revision does NOT automatically change `PublishedRevisionId`. The caller must publish explicitly.

### PostRevision
- Revisions are immutable. No update methods exist on `PostRevision`.
- Content is never modified after creation. A new revision must be created instead.
- `CreatedAt` is set once in the factory and never changed.

### Category & Tag
- `Slug` must be unique per `BlogId`. Enforced via unique index on `(BlogId, Slug)`.
- Cannot be physically deleted. Use soft delete (`DeletedAt`).

### Media
- `BlogId` is immutable after upload. Media cannot be moved between blogs.
- Deletion is soft. The file storage cleanup is a separate concern (not domain).

---

## Invariants (Explicit)

| # | Invariant | Enforced In | Violation |
|---|---|---|---|
| 1 | Post must have ≥ 1 revision | `Post` constructor | `DomainException` |
| 2 | Published revision must belong to the post | `Post.Publish()` | `DomainException` |
| 3 | Post slug must be unique per blog | DB unique index + app-layer pre-check | `DomainException` |
| 4 | Blog subdomain must be globally unique | DB unique index + app-layer pre-check | `DomainException` |
| 5 | BlogAdmin cannot access data of another blog | `TenantContext` + domain method guard | `TenantAccessException` |
| 6 | Subdomain format: lowercase alphanumeric + hyphens | `Tenant` constructor | `DomainException` |
| 7 | Slug format: lowercase alphanumeric + hyphens | `Post`, `Category`, `Tag` constructors | `DomainException` |
| 8 | PostRevision is immutable after creation | No mutating methods on `PostRevision` | N/A (compile-time) |
| 9 | BlogId on any tenant-scoped entity is `private init` | Property access modifier | N/A (compile-time) |
| 10 | Soft-deleted entities excluded from all public queries | Repository implementations | Silent (filter applied) |

---

## Enums

```csharp
public enum PostStatus
{
    Draft,
    Published
}
```

---

## Aggregate Boundary Summary

| Aggregate | Root | Owned Children | Cross-Aggregate References |
|---|---|---|---|
| Tenant | `Tenant` | — | `ApplicationUser.Id` (OwnerId) |
| Post | `Post` | `PostRevision[]` | `Tenant.Id` (BlogId) |
| Category | `Category` | — | `Tenant.Id` (BlogId) |
| Tag | `Tag` | — | `Tenant.Id` (BlogId) |
| Media | `Media` | — | `Tenant.Id` (BlogId) |

Cross-aggregate references are by `Guid` ID only. Navigation properties across aggregate roots are never used in domain logic.

