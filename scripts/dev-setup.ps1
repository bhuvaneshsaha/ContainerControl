$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

function Require-Command {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$InstallHint
    )
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Missing prerequisite: $Name. $InstallHint"
    }
}

Require-Command "dotnet" "Install the .NET 10 SDK from https://dotnet.microsoft.com/download"
Require-Command "node" "Install Node.js 22 LTS from https://nodejs.org/"
Require-Command "docker" "Install Docker Engine. Docker Desktop is not used."

$dotnetVersion = (& dotnet --version).Trim()
if ($dotnetVersion -notmatch "^10\.") {
    throw ".NET 10 SDK is required. Found $dotnetVersion."
}

$nodeVersion = (& node --version).Trim()
if ($nodeVersion -notmatch "^v22\.") {
    throw "Node.js 22 LTS is required. Found $nodeVersion."
}

& docker info | Out-Null
& docker compose version | Out-Null
& docker compose -f (Join-Path $root "deploy/local/compose.yaml") up -d --wait postgres

Write-Host "PostgreSQL is ready."
Write-Host "API: dotnet run --project src/Host/ContainerControl.Host.csproj"
Write-Host "SPA: npm start --prefix client"
Write-Host "Infisical and Traefik profiles are not started."
