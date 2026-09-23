---
name: Enterprise DevOps
description: Use when adding or reviewing CI/CD, Docker, Kubernetes, environments, pipeline secrets, health/observability exporters, PWA cache headers, and release gates for a .NET + Angular enterprise app. Do not choose the cloud — Architecture already did.
---
# Enterprise DevOps

## Goal
Make the app shippable: build, test, containerize, deploy through environments, with secrets and gates — production-ready, not a demo pipeline.

## When to use
CI/CD, Docker, Kubernetes, environment promotion, pipeline secrets, health checks, observability exporters, or release gates. Follow the Architecture platform skill already chosen (On-prem / Azure / AWS). Do not invent a second cloud.

## Defaults
- **Azure:** Azure Pipelines and/or GitHub Actions; ACR; App Service / Container Apps / AKS as architecture decided
- **AWS:** GitHub Actions (or CodeBuild/CodePipeline if the user already uses them); ECR; ECS/EKS as architecture decided
- **On-prem:** self-hosted runners; Docker on VMs or existing Kubernetes; IIS/Kestrel behind a reverse proxy
- Multi-stage Dockerfile for the API and for Angular (or Ionic) static output; non-root user; no secrets in layers
- Environments: Dev / Test / Prod with promotion; seed jobs **Dev/Test only**
- Health: pipeline waits on `/health/ready` (or `/health` if live/ready not split) after deploy — `skills/enterprise-observability.md`
- Observability: ship OTel exporters / platform logs already chosen in architecture; no paid APM
- PWA: cache headers so `index.html` and `ngsw.json` revalidate; hashed assets may be immutable — `skills/enterprise-pwa-offline.md`
- Release gates: tests + lint + permission that production secrets are not in the repo
- Open-source or platform-built-in only — no commercial CD products

## Steps
1. Confirm platform skill (On-prem / Azure / AWS) and existing pipeline files.
2. Add or extend CI: restore, build, test, lint. Fail the build on test failure.
3. Add container build/publish when the host is containerized.
4. Wire CD per environment; use pipeline secret variables / Key Vault / Secrets Manager / env — never commit values.
5. Gate production: tests green, manual or automated approval if the user wants it, health check after deploy.
6. Document how to run the pipeline and how to run the container locally.

## Quality bar
- No secrets, connection strings, or signing keys in YAML
- Production cannot run Development seeders
- Deterministic builds (lockfiles)
- Pipeline is readable for newcomers

## Report back
- Pipeline and Dockerfile paths
- Environments and gates
- How to run locally
- Residual risks
