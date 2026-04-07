# Blogify Deployment Guide

## Required GitHub Actions Secrets

| Secret | Description |
|---|---|
| `VPS_HOST` | Public IP address of the VPS |
| `VPS_USER` | SSH username (usually `ubuntu`) |
| `VPS_SSH_KEY` | Private SSH key — the VPS must have the matching public key in `~/.ssh/authorized_keys` |
| `GHCR_READ_TOKEN` | GitHub classic PAT with `read:packages` scope — used by the VPS to pull images from ghcr.io |
| `CF_DNS_API_TOKEN` | Cloudflare API token with `Zone:DNS:Edit` permission scoped to `lunavex.com` |
| `POSTGRES_PASSWORD` | Strong random password for the PostgreSQL superuser |
| `POSTGRES_USER` | PostgreSQL username (e.g. `blogify`) |
| `POSTGRES_DB` | PostgreSQL database name (e.g. `blogdb`) |
| `IP_HASH_SALT` | Random string used for analytics IP hashing |
| `PGADMIN_DEFAULT_EMAIL` | pgAdmin login email |
| `PGADMIN_DEFAULT_PASSWORD` | Strong pgAdmin login password |

---

## DNS Records (Cloudflare)

Create the following **A** records in the Cloudflare dashboard.  
Set **Proxy status** to **DNS only** (grey cloud) for all four records — Traefik handles TLS termination directly.

| Type | Name | Value |
|---|---|---|
| A | `blog.lunavex.com` | `<VPS_IP>` |
| A | `www.blog.lunavex.com` | `<VPS_IP>` |
| A | `*.blog.lunavex.com` | `<VPS_IP>` |
| A | `pgadmin.blog.lunavex.com` | `<VPS_IP>` |

---

## First-Deploy Steps

1. **Provision the VPS** — SSH in as root (or a sudo-capable user) and run:
   ```bash
   bash <(curl -fsSL https://raw.githubusercontent.com/<your-org>/blogify/main/scripts/setup-vps.sh)
   ```
   Alternatively, copy `scripts/setup-vps.sh` to the server and run it directly.

2. **Copy configuration files** to `/opt/blogify/` on the VPS:
   ```bash
   scp docker-compose.yml ubuntu@<VPS_IP>:/opt/blogify/
   scp traefik/traefik.yml ubuntu@<VPS_IP>:/opt/blogify/traefik/
   ```

3. **Create the `.env` file** on the VPS from the example:
   ```bash
   cp .env.example /opt/blogify/.env
   # Edit /opt/blogify/.env with real production values
   ```

4. **Start the stack** for the first time:
   ```bash
   ssh ubuntu@<VPS_IP> "cd /opt/blogify && docker network create web || true && docker compose up -d"
   ```

5. **Trigger automated deploys** by publishing a GitHub Release.  
   The CD workflow builds the Docker image, pushes it to ghcr.io, and deploys it to the VPS automatically.

---

## Post-Deploy Security

- **Change the default SuperAdmin password immediately** — log in at `https://blog.lunavex.com/sa` using the seeded credentials (`superadmin@blogify.com` / `SuperAdmin123A+`) and change the password before exposing the site publicly.
- Rotate `POSTGRES_PASSWORD`, `PGADMIN_DEFAULT_PASSWORD`, and `IP_HASH_SALT` before going live.
- Ensure `CF_DNS_API_TOKEN` is scoped only to `Zone:DNS:Edit` on `lunavex.com` — no broader permissions needed.
