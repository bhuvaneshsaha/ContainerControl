---
name: Enterprise architecture Azure
description: Use when the architecture specialist is targeting Microsoft Azure (App Service, Container Apps, AKS, Azure SQL, Key Vault). Do not use unless Azure is the chosen platform.
---
# Enterprise architecture Azure

## Goal
Map a Modular Monolith .NET + Angular (PWA if chosen; Ionic only if native shell) system onto Azure using proven, preferably OSS-friendly Azure services.

## When to use
The user chose **Azure**. If the target is on-premises or AWS, stop and use that platform skill instead.

## Defaults
- Compute: Azure App Service or Container Apps for the monolith; AKS only if the user already needs Kubernetes
- Data: Azure SQL or PostgreSQL Flexible Server; Cosmos DB only when a module truly needs a document store
- Secrets: Azure Key Vault + managed identity; never keys in source
- Frontend: Azure Static Web Apps or App Service for the Angular SPA/PWA; same-site or BFF if cookies. Ionic only if Architecture chose a native shell
- PWA hosting: never long-cache `index.html` or `ngsw.json`; hashed assets may be immutable. Follow `skills/enterprise-pwa-offline.md` when the client is a PWA
- Observability: OpenTelemetry into Azure Monitor / App Insights (platform exporter, not a third-party paid APM). Follow `skills/enterprise-observability.md`
- CI/CD: GitHub Actions or Azure DevOps — OSS pipelines, no paid extra products
- Identity: Entra ID is an OIDC *option* for the Auth specialist, not an architecture default
- Open-source app stack; use Azure PaaS, not commercial third-party SaaS as a default

## Steps
1. Confirm region, environments (dev/test/prod), and private-network needs (VNet, Private Link).
2. Keep one App Service / Container Apps app for the Modular Monolith unless a module must isolate.
3. Plan Key Vault, managed identity, and configuration (App Configuration optional).
4. Outline ingress, TLS, CORS, and how the SPA/PWA is hosted (Static Web Apps vs App Service vs API same-origin).
5. Write a short ADR: Azure targets chosen and what was rejected (AKS vs App Service, SQL vs Cosmos, SWA vs App Service for the client).

## Report back
- Resource list (names as types, not invented SKUs/prices)
- Identity, secrets, and networking sketch
- Risks and open questions
