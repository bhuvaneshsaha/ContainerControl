#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"

require() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing prerequisite: $1. $2" >&2
    exit 1
  fi
}

require dotnet "Install the .NET 10 SDK from https://dotnet.microsoft.com/download"
require node "Install Node.js 22 LTS from https://nodejs.org/"
require docker "Install Docker Engine. Docker Desktop is not used."

dotnet_version="$(dotnet --version)"
if [[ ! "$dotnet_version" =~ ^10\. ]]; then
  echo ".NET 10 SDK is required. Found $dotnet_version." >&2
  exit 1
fi

node_version="$(node --version)"
if [[ ! "$node_version" =~ ^v22\. ]]; then
  echo "Node.js 22 LTS is required. Found $node_version." >&2
  exit 1
fi

docker info >/dev/null
docker compose version >/dev/null

docker compose -f "$root/deploy/local/compose.yaml" up -d --wait postgres

echo "PostgreSQL is ready."
echo "API: dotnet run --project src/Host/ContainerControl.Host.csproj"
echo "SPA: npm start --prefix client"
echo "Infisical and Traefik profiles are not started."
