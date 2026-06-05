# LegalSynq

A multi-tenant legal and healthcare platform built with .NET 8 microservices and Next.js 15.

## What It Does

LegalSynq provides an integrated suite of products for legal firms, healthcare providers, and financial organizations:

- **SynqLien** — Medical lien lifecycle management (creation, marketplace listing, offers, purchase, servicing, settlement)
- **CareConnect** — Healthcare provider directory, referral management, and appointment scheduling
- **SynqFund** — Funding application workflow (submission, review, approval)
- **SynqAudit** — Platform-wide audit trail, user activity monitoring, and compliance investigation
- **Notifications** — Multi-channel notification delivery (email via SendGrid, SMS via Twilio, SMTP)
- **Comms** — Internal communications and document-linked messaging between platform participants
- **Flow** — Workflow and task orchestration with SLA tracking, work distribution, and assignment recommendations
- **Monitoring** — Platform health monitoring, uptime aggregation, alerting, and service status tracking
- **Task** — Standalone task management service for tracking and assigning work items across the platform
- **Reports** — Scheduled and on-demand reporting with delivery via email, SFTP, and cloud storage

## Architecture

```
┌──────────────────┐    ┌──────────────────┐
│   Tenant App     │    │  Control Center  │
│  (Next.js 15)    │    │  (Next.js 15)    │
│   Port 5000      │    │   Port 5004      │
└────────┬─────────┘    └────────┬─────────┘
         │                       │
         └───────────┬───────────┘
                     │
              ┌──────┴──────┐
              │   Gateway   │
              │   (YARP)    │
              │  Port 5010  │
              └──────┬──────┘
                     │
    ┌────────────────┼────────────────────────────┐
    │                │                            │
┌───┴───┐  ┌────────┴────┐  ┌───────┴───────┐  ┌─┴──────┐
│Identity│  │  CareConnect │  │     Liens     │  │ Tenant │
│ :5001  │  │    :5003     │  │     :5009     │  │ :5005  │
└────────┘  └──────────────┘  └───────────────┘  └────────┘
    │                │                │
┌───┴───┐  ┌────────┴────┐  ┌───────┴───────┐
│ Fund  │  │ Notifications│  │   Documents   │
│ :5002 │  │    :5008     │  │     :5006     │
└───────┘  └──────────────┘  └───────────────┘
                     │
              ┌──────┴──────┐
              │    Audit    │
              │    :5007    │
              └──────┬──────┘
                     │
    ┌────────────────┼────────────────────────────┐
    │                │                            │
┌───┴───┐  ┌────────┴────┐  ┌───────┴───────┐  ┌─┴──────┐
│ Comms │  │    Flow     │  │  Monitoring   │  │ Task   │
│ :5011 │  │   :5012     │  │    :5015      │  │ :5016  │
└───────┘  └─────────────┘  └───────────────┘  └────────┘
    │
┌───┴──────┐  ┌────────────────┐
│ Reports  │  │ Artifacts API  │
│  :5029   │  │    :5020       │
└──────────┘  └────────────────┘
```

### Service Layering

Each .NET microservice follows clean architecture:

```
Service.Api/              → Endpoints, middleware, startup
Service.Application/      → Interfaces, DTOs, services
Service.Domain/           → Entities, enums, value objects
Service.Infrastructure/   → DbContext, repositories, external integrations
```

### Shared Libraries

- **BuildingBlocks** — Base types (`AuditableEntity`), request context, authorization constants
- **Contracts** — Shared response models (`ServiceResponse<T>`, `HealthResponse`)
- **AuditClient** — Reusable audit event publishing client

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 15.2.9, React 18, TypeScript, Tailwind CSS |
| Flow Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS (standalone app) |
| Gateway | ASP.NET Core + YARP reverse proxy |
| Services | ASP.NET Core 8.0 Minimal APIs |
| ORM | Entity Framework Core 8.0 (Pomelo MySQL) |
| Database | MySQL 8.0 |
| Auth | JWT Bearer tokens, BFF session cookies |
| Email | SendGrid, SMTP (MailKit) |
| SMS | Twilio |
| Artifacts API | Node.js + TypeScript (ts-node-dev) |

## Multi-Tenancy

Every request carries tenant context through JWT claims. Entities are scoped by `TenantId` and queries enforce tenant isolation at the repository level. The Identity service manages tenants, organizations, users, roles, and product entitlements.

### Role-Based Access

- **Platform roles** — `PlatformAdmin`, `TenantAdmin`, `StandardUser`
- **Product roles** — `CARECONNECT_REFERRER`, `CARECONNECT_RECEIVER`, `SYNQFUND_REFERRER`, `SYNQFUND_FUNDER`, `SYNQLIEN_SELLER`, `SYNQLIEN_BUYER`, `SYNQLIEN_HOLDER`
- **Access Groups** — Inherited role assignments through group membership

## Getting Started

### Prerequisites

- .NET SDK 8.0
- Node.js 22
- MySQL 8.0

### Running Locally

The project includes a dev script that builds and starts all services in parallel:

```bash
bash scripts/run-dev.sh
```

This starts:

| Component | Port |
|---|---|
| Tenant App (Next.js) | 5000 |
| Identity Service | 5001 |
| Fund Service | 5002 |
| CareConnect Service | 5003 |
| Control Center (Next.js) | 5004 |
| Tenant Service | 5005 |
| Documents Service | 5006 |
| Audit Service | 5007 |
| Notifications Service | 5008 |
| Liens Service | 5009 |
| Gateway (YARP) | 5010 |
| Comms Service | 5011 |
| Flow Service (backend) | 5012 |
| Monitoring Service | 5015 |
| Task Service | 5016 |
| Artifacts API | 5020 |
| Reports Service | 5029 |

The Flow frontend (`apps/services/flow/frontend`) is a standalone Next.js app that is **not** started by `run-dev.sh`. Start it separately:

| Component | Port |
|---|---|
| Flow Frontend (Next.js) | 3000 |

```bash
cd apps/services/flow/frontend && npm run dev
```

### Environment Variables

Each service reads its configuration from `appsettings.json` and `appsettings.Development.json`. Connection strings are configured per service (e.g., `ConnectionStrings:IdentityDb`, `ConnectionStrings:LiensDb`). JWT signing keys are shared across services for token validation.

#### Startup Probe Timeouts (`scripts/run-prod.sh`)

| Variable | Default | Description |
|---|---|---|
| `PROBE_TIMEOUT_NODEJS` | `60` | Seconds to wait for Node.js services (Web, Proxy, Control Center, Artifacts) to respond to their health endpoint before logging a warning. |
| `PROBE_TIMEOUT_DOTNET` | `90` | Seconds to wait for .NET services (Identity, Fund, CareConnect, Documents, Audit, Notifications, Liens, Gateway, Flow, Comms, Monitoring, Task, Reports, Tenant) to respond to their health endpoint before logging a warning. |

Set these in the deployment environment to tune probe deadlines without editing the script.

## Project Structure

```
├── apps/
│   ├── web/                    # Tenant-facing Next.js app
│   ├── control-center/         # Internal admin Next.js app
│   ├── gateway/                # YARP reverse proxy
│   └── services/
│       ├── identity/           # Auth, users, tenants, RBAC
│       ├── fund/               # Funding applications
│       ├── careconnect/        # Providers, referrals, appointments
│       ├── liens/              # Lien lifecycle, offers, bills of sale
│       ├── documents/          # Document storage
│       ├── notifications/      # Email, SMS delivery
│       ├── audit/              # Audit event logging and querying
│       ├── tenant/             # Tenant configuration and branding
│       ├── comms/              # Internal communications and messaging
│       ├── flow/               # Workflow orchestration (backend + standalone frontend)
│       │   ├── backend/        # .NET Flow API (port 5012)
│       │   └── frontend/       # Standalone Next.js Flow UI
│       ├── monitoring/         # Health monitoring, uptime, and alerting
│       ├── task/               # Task management and assignment
│       └── reports/            # Scheduled and on-demand reporting
├── artifacts/
│   └── api-server/             # Node.js artifacts API server (port 5020)
├── shared/
│   ├── building-blocks/        # Base types and utilities
│   ├── contracts/              # Shared API contracts
│   └── audit-client/           # Audit event publisher
└── scripts/
    └── run-dev.sh              # Dev startup orchestrator
```

## Frontend Apps

### Tenant App (`apps/web`)

The main application for end users. Uses a BFF (Backend for Frontend) pattern where server-side route handlers proxy API calls to the gateway with Bearer tokens. Client-side auth is session-cookie based — the frontend never handles raw JWTs.

### Control Center (`apps/control-center`)

Internal administration portal for platform operators. Provides tenant management, cross-tenant user administration, audit investigation, and system monitoring. Requires `PlatformAdmin` role.

### Flow Frontend (`apps/services/flow/frontend`)

Standalone Next.js 16 application for the Flow workflow product. Runs independently from the main tenant app and connects to the Flow backend service via the gateway.

## License

Proprietary. All rights reserved.
