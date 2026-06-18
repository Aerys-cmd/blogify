# Blogify

Blogify is a multi-tenant blogging platform for creators. It is built with ASP.NET Core Razor Pages on .NET 10, EF Core SQLite, ASP.NET Identity, localized English/Turkish UI, tenant-aware public blogs, per-blog administration, and themeable public sites.

## Main Features

- Multi-blog tenancy with public blogs on subdomains and admin pages under `/app/admin/{blogSlug}`.
- Owner and member access using per-blog roles: `Writer`, `Editor`, and `Admin`.
- Public themes: `default`, `minimal`, and `aurora`.
- BlockNote-based post editor with server-side public rendering and searchable content extraction.
- Categories, tags, media library, comments, moderation, RSS, sitemap, analytics, and public output caching.
- Local file storage by default, with optional Cloudflare R2 storage when all R2 settings are configured.
- Localized email templates for password reset and blog invitations.
- SuperAdmin area under `/sa`.

## Repository Layout

```text
Blogify.Web/              ASP.NET Core Razor Pages application
Blogify.Tests/            xUnit test project
Blogify.AppHost/          .NET Aspire local orchestration with Traefik
Blogify.ServiceDefaults/  shared health, telemetry, resilience, and discovery defaults
```

Useful supporting files:

- `.github/copilot-instructions.md`: agent/Codex operating guide.
- `.github/instructions/`: file-scoped coding instructions.
- `.github/architecture/system-overview.md`: tenancy, routing, authorization, theme, and data-boundary reference.
- `PRODUCT.md` and `DESIGN.md`: product and UI design direction.
- `.env.example`: production Docker Compose environment template.

## Prerequisites

- .NET 10 SDK
- Node.js 20 or newer for frontend asset builds
- Docker, if running Aspire with Traefik or using Docker Compose deployment

The repository pins the .NET SDK through `global.json`.

## Local Development

Restore, build, and test:

```bash
dotnet restore Blogify.sln
dotnet build Blogify.Web/Blogify.Web.csproj --no-restore
dotnet test Blogify.Tests/Blogify.Tests.csproj --no-restore
```

Run the app through Aspire:

```bash
dotnet run --project Blogify.AppHost
```

`Blogify.AppHost` starts the web app on a fixed HTTP endpoint and a Traefik container for local subdomain routing. Use the Aspire dashboard output to find the exact local URLs.

The web app runs EF migrations and seed data at startup. Development SQLite defaults to `Blogify.Web/blogify.db`.

## Frontend Assets

The application is primarily server-rendered Razor. React is used only for the BlockNote editor in `Blogify.Web/ClientApp`.

Install and build assets from `Blogify.Web`:

```bash
npm ci
npm run build:editor
npm run build:css:default
npm run build:css:minimal
npm run build:css:aurora
```

The Dockerfile enables both Tailwind and Vite build targets during publish. For local .NET builds, run the relevant npm scripts after editing `ClientApp/` or `styles/themes/`.

## Configuration

The required database connection key is `ConnectionStrings:blogdb`.

Important configuration sections:

- `Tenant:PlatformHosts`: root/platform hosts that are not treated as tenant subdomains.
- `Storage`: image quality settings and optional `Storage:R2` credentials. R2 is enabled only when account id, access key, secret, bucket, and public base URL are all present.
- `Email`: enables/disables queued email delivery and sets public URL/from metadata.
- `Smtp`: SMTP host, port, credentials, and SSL mode.
- `Analytics:IpHashSalt`: salt used for analytics IP hashing.
- `FeedbackHub:PublicKey`: enables the Blog Admin Feedback Hub widget when set.
- `DataProtection:KeysPath`: optional override for persisted data-protection keys.

Development email delivery is disabled by default. Production Docker Compose enables email by default and requires SMTP settings.

## Docker Deployment

The repository includes a production `Dockerfile` and `docker-compose.yml` that publishes the web app, builds frontend assets, stores SQLite/uploads/data-protection keys in volumes, and exposes the app behind an external Traefik network named `web`.

Create `.env` from `.env.example`, then run:

```bash
docker compose up -d
```

The compose file expects the image at `ghcr.io/${OWNER}/blogify-web:${IMAGE_TAG}` and currently routes `blogify.com.tr`, `www.blogify.com.tr`, and wildcard subdomains through Traefik.

## CI/CD

GitHub Actions include:

- CI on pushes and pull requests: restore, build, and test.
- CD on published releases: build and push the Docker image to GHCR, copy `docker-compose.yml` to the VPS, and restart the service over SSH.
