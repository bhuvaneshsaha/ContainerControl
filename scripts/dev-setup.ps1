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
$profiles = @()
for ($i = 0; $i -lt $args.Count; $i++) {
    if ($args[$i] -eq "--profile") {
        if ($i + 1 -ge $args.Count) {
            throw "--profile requires a name."
        }
        $profiles += $args[$i + 1]
        $i++
        continue
    }
    throw "Unknown argument: $($args[$i])"
}

$composeFile = Join-Path $root "deploy/local/compose.yaml"
if ($profiles.Count -eq 0) {
    & docker compose -f $composeFile up -d --wait postgres
    Write-Host "PostgreSQL is ready."
    Write-Host "Infisical and Traefik profiles were not started. Pass --profile infisical or --profile traefik when that step starts."
} else {
    $composeArgs = @()
    foreach ($profile in $profiles) {
        $composeArgs += @("--profile", $profile)
    }
    & docker compose @composeArgs -f $composeFile up -d --wait
    Write-Host "Compose profiles are up: $($profiles -join ', ')"
}

Write-Host "API: dotnet run --project src/Host/ContainerControl.Host.csproj"
Write-Host "SPA: npm start --prefix client"
