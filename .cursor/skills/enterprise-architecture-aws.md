---
name: Enterprise architecture AWS
description: Use when the architecture specialist is targeting Amazon Web Services (ECS/EKS, RDS, Secrets Manager). Do not use unless AWS is the chosen platform.
---
# Enterprise architecture AWS

## Goal
Map a Modular Monolith .NET + Angular (PWA if chosen; Ionic only if native shell) system onto AWS using proven, preferably OSS-friendly AWS services.

## When to use
The user chose **AWS**. If the target is on-premises or Azure, stop and use that platform skill instead.

## Defaults
- Compute: ECS/Fargate (or Elastic Beanstalk) for the monolith; EKS only if the user already needs Kubernetes
- Data: Amazon RDS (SQL Server or PostgreSQL); DynamoDB only when a module truly needs a document/key-value store
- Secrets: AWS Secrets Manager or SSM Parameter Store + IAM roles; never keys in source
- Frontend: S3 + CloudFront for the Angular SPA/PWA, or serve from the API host if same-site cookies. Ionic only if Architecture chose a native shell
- PWA hosting: CloudFront must revalidate `index.html` and `ngsw.json`; hashed assets may be immutable. Follow `skills/enterprise-pwa-offline.md` when the client is a PWA
- Observability: OpenTelemetry into CloudWatch (and/or X-Ray if already standard). Follow `skills/enterprise-observability.md`
- CI/CD: GitHub Actions or the user's existing pipeline — no commercial extra products
- Identity: Amazon Cognito is an OIDC *option* for the Auth specialist, not an architecture default
- Open-source app stack; use AWS PaaS, not commercial third-party SaaS as a default

## Steps
1. Confirm region, environments (dev/test/prod), and VPC / private-subnet needs.
2. Keep one ECS service (or equivalent) for the Modular Monolith unless a module must isolate.
3. Plan IAM task roles, secrets, and configuration.
4. Outline ALB/CloudFront, TLS, CORS, and how the SPA/PWA is hosted.
5. Write a short ADR: AWS targets chosen and what was rejected (ECS vs EKS, RDS vs DynamoDB).

## Report back
- Resource list (names as types, not invented pricing)
- IAM, secrets, and networking sketch
- Risks and open questions
