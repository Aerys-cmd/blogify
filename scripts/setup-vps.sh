#!/usr/bin/env bash
set -euo pipefail

# Setup script for fresh Ubuntu 22.04 VPS
# Run once as root or a sudo-capable user.

echo "==> Updating system packages..."
apt-get update && apt-get upgrade -y

echo "==> Installing Docker..."
curl -fsSL https://get.docker.com | sh

TARGET_USER="${1:-${SUDO_USER:-${USER:-}}}"
if [ -z "$TARGET_USER" ] || [ "$TARGET_USER" = "root" ]; then
  echo "ERROR: Unable to determine the non-root user to add to the docker group."
  echo "Re-run this script with the deploy username as the first argument, for example:"
  echo "  sudo ./scripts/setup-vps.sh myuser"
  exit 1
fi

echo "==> Adding '$TARGET_USER' to the docker group..."
usermod -aG docker "$TARGET_USER"
echo "NOTE: Group membership changes require a new login/session before Docker can be used without sudo."
echo "==> Creating deployment directory..."
mkdir -p /opt/blogify/traefik

echo "==> Creating external Docker network 'web'..."
docker network create web || echo "Network 'web' already exists, skipping."

echo ""
echo "======================================================"
echo " Setup complete!"
echo "======================================================"
echo " Next steps:"
echo "  1. Copy docker-compose.yml, traefik/traefik.yml, and"
echo "     .env to /opt/blogify/"
echo "  2. cd /opt/blogify && docker compose up -d"
echo "======================================================"
