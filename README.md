# blogify

> ASP.NET Core blog platform orchestrated with .NET Aspire — Docker, Traefik, GitHub Actions CI/CD.

---

## What is blogify?

blogify is a content-focused blog platform built on ASP.NET Core, using .NET Aspire for local
orchestration and multi-container deployment. The project demonstrates modern .NET application
structure: a shared service defaults layer, an Aspire AppHost as the composition root, and a
Razor Pages / MVC web application — all wired together with Docker and Traefik for production.

---

## Architecture

```
Blogify.AppHost/          ← .NET Aspire orchestration entry point
Blogify.Web/              ← Razor Pages / MVC web application
Blogify.ServiceDefaults/  ← Shared telemetry, health checks, resilience defaults
```

**.NET Aspire** acts as the composition root: `Blogify.AppHost` declares all resources (the web
app, databases, and any dependent services) and wires them together. Running the AppHost starts
everything with a single `dotnet run`, including the Aspire developer dashboard.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core 9 |
| Language | C# 13, .NET 9 |
| Orchestration | .NET Aspire |
| Deployment | Docker, Traefik, GitHub Actions |
| Styling | Razor Pages / MVC views |

---

## Running Locally

**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Docker](https://docs.docker.com/get-docker/)

```bash
git clone https://github.com/Aerys-cmd/blogify.git
cd blogify

# Run via .NET Aspire (recommended — starts all dependencies automatically)
dotnet run --project Blogify.AppHost
```

The Aspire dashboard will be available at `https://localhost:15888`.  
The web application will be available at the port shown in the dashboard.

## Email Delivery

Password-reset and blog-invitation emails are rendered as localized HTML and placed on a bounded
in-memory queue. Development defaults to disabled delivery, which logs and discards queued email.
Production enables SMTP delivery by default.

For Docker Compose deployments, create a `.env` file beside `docker-compose.yml` from
`.env.example`. Compose maps these deployment variables to the application's ASP.NET Core
configuration:

```bash
FEEDBACK_HUB_PUBLIC_KEY=...
EMAIL_ENABLED=true
EMAIL_PUBLIC_BASE_URL=https://blogify.example.com
EMAIL_FROM_ADDRESS=no-reply@blogify.example.com
EMAIL_FROM_NAME=Blogify
EMAIL_QUEUE_CAPACITY=100
SMTP_HOST=smtp.example.com
SMTP_PORT=587
SMTP_USERNAME=no-reply@blogify.example.com
SMTP_PASSWORD=...
SMTP_USE_SSL=false
```

`FEEDBACK_HUB_PUBLIC_KEY` enables the Feedback Hub widget in the Blog Admin interface. The widget
is omitted when the key is empty.

`EMAIL_PUBLIC_BASE_URL` is used for canonical links in email. `SMTP_USE_SSL=true` selects
SSL-on-connect; `false` selects STARTTLS. Failed SMTP deliveries are retried after 2, 8, and 30
seconds, then logged and discarded. The queue applies backpressure when full and is not persistent,
so queued messages are lost when the application restarts.

---

## Deployment

The repository includes a `Dockerfile` and `docker-compose.yml` for production deployment, along
with a Traefik configuration for TLS termination and reverse proxying.

```bash
docker compose up -d
```

See the `docs/` directory for detailed deployment and configuration notes.

---

## License

MIT — see [LICENSE](LICENSE) if present, otherwise all rights reserved.
