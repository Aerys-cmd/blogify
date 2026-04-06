# Domain Model — Blogify

## Aggregate Map

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  Tenant (Blog) Aggregate                                                    │
│  Root: Tenant                                                               │
│  ───────────────────────────────────────────────────────────────────────    │
│  Tenant { Id, Title, Subdomain, OwnerId, ActiveTheme, CreatedAt, DeletedAt }│
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  Post Aggregate                                                             │
│  Root: Post                                                                 │
│  ───────────────────────────────────────────────────────────────────────    │
│  Post { Id, BlogId, AuthorId, Slug, Excerpt, CoverImageId, Status,          │
│         PublishedRevisionId, CreatedAt, DeletedAt }                         │
│    └── PostRevision[] { Id, PostId, Title, Content, CreatedAt }             │
│    └── PostCategory[] { PostId, CategoryId }  (join entity)                 │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  Category Aggregate                                                         │
│  Root: Category                                                             │
│  ────────────────────────────────────────────────────────────────���──────    │
│  Category { Id, BlogId, Name, Slug, CreatedAt, DeletedAt }                  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  Tag Aggregate                                                              │
│  Root: Tag                                                                  │
│  ───────────────────────────────────────────────────────────────────────    │
│  Tag { Id, BlogId, Name, Slug, CreatedAt, DeletedAt }                       │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  Media Aggregate                                                            │
│  Root: Media                                                                │
│  ───────────────────────────────────────────────────────────────────────    │
│  Media { Id, BlogId, FileName, Url, ContentType, SizeBytes,                 │
│          AltText, Title, Description, ThumbnailUrl, WidthPx, HeightPx,      │
│          UploadedAt, DeletedAt }                                            │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  Comment Aggregate                                                          │
│  Root: Comment                                                              │
│  ───────────────────────────────────────────────────────────────────────    │
│  Comment { Id, BlogId, PostId, AuthorId, Content, ParentCommentId,          │
│            CreatedAt, DeletedAt }                                           │
└─────────────────────────────────────────────────────────────────────────────┘
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
| `ActiveTheme` | `string` | Theme name, max 50 chars, default `"default"`. Valid values: `default`, `minimal`, `aurora`. |
| `CreatedAt` | `DateTimeOffset` | Set on creation, immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### Post
A piece of content published under a blog. Identity is the aggregate root.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `BlogId` | `Guid` | FK to `Tenant.Id`, `private init` |
| `AuthorId` | `string` | FK to `ApplicationUser.Id`, max 450 chars |
| `Slug` | `string` | Unique per blog, URL-safe, max 300 chars |
| `Excerpt` | `string?` | Optional summary, max 500 chars |
| `CoverImageId` | `Guid?` | FK to `Media.Id`, nullable, set-null on delete |
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

### PostCategory
Explicit many-to-many join entity owned by the Post aggregate.

| Property | Type | Notes |
|---|---|---|
| `PostId` | `Guid` | Part of composite PK, FK to `Post.Id` |
| `CategoryId` | `Guid` | Part of composite PK, FK to `Category.Id` |

Managed exclusively via `Post.SetCategories(IEnumerable<Guid> categoryIds)`. Never create or remove `PostCategory` records directly.

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

> **Note:** `Tag` does not have an EF Core global query filter. Queries must include `.Where(t => t.BlogId == blogId && t.DeletedAt == null)` explicitly.

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
| `AltText` | `string?` | Accessibility alt text, max 500 chars |
| `Title` | `string?` | Optional display title, max 255 chars |
| `Description` | `string?` | Optional description, max 2000 chars |
| `ThumbnailUrl` | `string?` | URL of generated thumbnail (null if unsupported) |
| `WidthPx` | `int?` | Image width in pixels (null for non-images) |
| `HeightPx` | `int?` | Image height in pixels (null for non-images) |
| `UploadedAt` | `DateTimeOffset` | Immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### Comment
A user comment on a post, scoped to a single blog. Supports nested replies via `ParentCommentId`.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK, `private init` |
| `BlogId` | `Guid` | FK to `Tenant.Id`, `private init` |
| `PostId` | `Guid` | FK to `Post.Id`, `private init` |
| `AuthorId` | `string` | FK to `ApplicationUser.Id`, `private init`, max 450 chars |
| `Content` | `string` | Comment body, max 2000 chars |
| `ParentCommentId` | `Guid?` | FK to `Comment.Id` for nested replies; `null` for top-level comments |
| `CreatedAt` | `DateTimeOffset` | Immutable |
| `DeletedAt` | `DateTimeOffset?` | Soft delete marker |

### ApplicationUser
ASP.NET Identity user. Extended with optional blog membership.

| Property | Type | Notes |
|---|---|---|
| `Id` | `string` | Identity PK |
| `TenantId` | `Guid?` | FK to `Tenant.Id`. Tracks **non-owner membership** for BlogAdmin panel access. `null` for SuperAdmins and unassigned users. Ownership is tracked separately via `Tenant.OwnerId`. |
| Standard Identity fields | — | Email, PasswordHash, etc. |

---

## Relationships

```
ApplicationUser ──── (one) ───► Tenant           (OwnerId — owner)
ApplicationUser ──── (many) ──► Tenant           (TenantId — membership)
Tenant          ──── (many) ──► Post             (posts)
Tenant          ──── (many) ──► Category         (categories)
Tenant          ──── (many) ──► Tag              (tags)
Tenant          ──── (many) ──► Media            (media)
Tenant          ──── (many) ──► Comment          (comments)
Post            ──── (many) ──► PostRevision     (revisions, owned)
Post            ──── (many-to-many via PostCategory) ──► Category
Post            ──── (many) ──► Comment          (comments on the post)
Comment         ──── (self) ──► Comment          (ParentCommentId for nested replies)
```

---

## Business Rules

### Tenant (Blog)
- `Subdomain` must be globally unique. Enforced via unique index.
- `Subdomain` must match `^[a-z0-9-]+$`. Validated in aggregate constructor.
- A tenant can only be owned by one user (one-to-one with `BlogAdmin` user via `OwnerId`).
- `ActiveTheme` must be one of `default`, `minimal`, `aurora`. Enforced in `Tenant.ChangeTheme()`.
- Soft-deleted tenants are hidden from subdomain resolution immediately (global query filter on `Blogs` table).

### Post
- A post MUST have at least one `PostRevision` at all times.
- The first revision is created atomically with the post via `Post.Create(...)`.
- `Slug` must be unique per `BlogId`. Enforced via unique index on `(BlogId, Slug)`.
- `Slug` must match `^[a-z0-9-]+$`. Validated in aggregate constructor.
- A post can only be published by pointing `PublishedRevisionId` to one of its own revisions.
- Attempting to publish with a `revisionId` not belonging to the post throws `DomainException`.
- Unpublishing clears `PublishedRevisionId` and sets `Status = Draft`.
- Adding a revision does NOT automatically change `PublishedRevisionId`. The caller must publish explicitly.
- Category associations are managed atomically via `Post.SetCategories(IEnumerable<Guid> categoryIds)`.
- `Excerpt` is optional (max 500 chars). Set via `Post.UpdateExcerpt(string? excerpt)`.
- `CoverImageId` is optional. Set via `Post.SetCoverImage(Guid? mediaId)`.

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
- Metadata (`AltText`, `Title`, `Description`) can be updated via `Media.UpdateMetadata(...)`.
- `ThumbnailUrl`, `WidthPx`, `HeightPx` are optional and may be set at upload time by `IFileStorageService`.

### Comment
- `BlogId`, `PostId`, and `AuthorId` are immutable after creation.
- Content is limited to 2000 characters.
- `ParentCommentId` is optional. When set, it references another `Comment.Id` (nested reply). An empty `Guid` is treated as `null`.
- Cannot be physically deleted. Use soft delete via `Comment.SoftDelete()`.

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
| 10 | Soft-deleted entities excluded from all public queries | EF Core global query filters (Post, Category, Media, Comment) + explicit filters (Tag) | Silent (filter applied) |
| 11 | Tenant ActiveTheme must be a recognised theme | `Tenant.ChangeTheme()` | `DomainException` |
| 12 | Comment content must not exceed 2000 characters | `Comment` constructor | `ArgumentException` |

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
| Post | `Post` | `PostRevision[]`, `PostCategory[]` | `Tenant.Id` (BlogId), `ApplicationUser.Id` (AuthorId), `Media.Id` (CoverImageId) |
| Category | `Category` | — | `Tenant.Id` (BlogId) |
| Tag | `Tag` | — | `Tenant.Id` (BlogId) |
| Media | `Media` | — | `Tenant.Id` (BlogId) |
| Comment | `Comment` | — | `Tenant.Id` (BlogId), `Post.Id` (PostId), `ApplicationUser.Id` (AuthorId), `Comment.Id` (ParentCommentId) |

Cross-aggregate references are by `Guid` ID only. Navigation properties across aggregate roots are never used in domain logic.

