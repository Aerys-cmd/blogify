#!/usr/bin/env bash
set -euo pipefail

# Setup script for fresh Ubuntu 22.04 VPS
# Run once as root or a sudo-capable user.

echo "==> Updating system packages..."
apt-get update && apt-get upgrade -y

echo "==> Installing Docker..."
curl -fsSL https://get.docker.com | sh

echo "==> Adding current user to the docker group..."
usermod -aG docker "$USER"

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
