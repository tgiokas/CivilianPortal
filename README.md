# CitizenPortal — Ηλεκτρονικό Πρωτόκολλο

## Overview

**CitizenPortal** is a citizen-facing web application for submitting applications and uploading documents to the DMS (Document Management System). It is promoted as a feature of the main DMS platform.

Citizens authenticate via **GSIS/TaxisNet** (brokered through Keycloak), submit applications with attachments, and receive protocol numbers assigned by the DMS backend.

---

## Architecture

- **Separate Keycloak Realm** (`CitizenRealm`) — isolates citizen users from DMS staff (`DMSRealm`)
- **Own PostgreSQL database** — stores citizen users, applications, documents, and outbox messages
- **Outbox Pattern** — guarantees transactional consistency between DB writes and Kafka events
- **Kafka** — async buffer between CitizenPortal and DMS for scalability (handles 1000+ concurrent submissions)
- **DMS.Storage** — shared file storage (files uploaded via HTTP/Traefik)

---

## Tech Stack

- .NET 9
- Keycloak (CitizenRealm + GSIS OIDC)
- PostgreSQL
- Apache Kafka (Confluent)
- Serilog
- Clean Architecture (SOLID)
- Docker / Docker Compose

---

## Project Structure

```
CitizenPortal.sln
├── src/
│   ├── Domain/           # Entities, Enums, Repository Interfaces
│   ├── Application/      # Services, DTOs, Error Codes, Interfaces
│   ├── Infrastructure/   # EF Core, Repositories, Kafka, HTTP Clients
│   └── Api/              # Controllers, Middleware, Program.cs
├── error-codes/
│   └── errors.json       # PORTAL-001..PORTAL-013
└── docker-compose.yml
```

---

## Submission Flow (Outbox Pattern)

```
1. Citizen submits form + files
2. CitizenPortal uploads files to DMS.Storage → gets storageFileIds
3. Single DB transaction:
   ├── INSERT Application (status: submitted)
   ├── INSERT ApplicationDocuments (storageFileIds)
   └── INSERT OutboxMessage (citizen.application.submitted)
4. Return tracking ID to citizen ✅
5. [Background] OutboxProcessor polls OutboxMessages → publishes to Kafka
6. [Background] DMS consumes event → assigns protocol number → publishes back
7. [Background] ProtocolAssignedConsumer updates CitizenPortal DB
8. Citizen receives email notification with protocol number
```

---

## Kafka Topics

| Topic | Producer | Consumer |
|-------|----------|----------|
| `citizen.application.submitted` | CitizenPortal (via Outbox) | DMS.Document |
| `citizen.application.protocol-assigned` | DMS.Document | CitizenPortal |
| `citizen.notification.email` | CitizenPortal | Email Service |

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `PORTAL_DB_CONNECTION` | PostgreSQL connection string |
| `KEYCLOAK_AUTHORITY` | Keycloak CitizenRealm URL |
| `KEYCLOAK_CLIENTID` | Keycloak client ID |
| `KAFKA_BOOTSTRAP_SERVERS` | Kafka broker address |
| `DMS_STORAGE_URL` | DMS.Storage service URL |
| `PORTAL_KAFKA_PROTOCOL_TOPIC` | Topic for protocol assignments |
| `PORTAL_KAFKA_CONSUMER_GROUP` | Kafka consumer group ID |

---

## Running

```bash
docker-compose up -d
```

API available at: `http://localhost:5040`
Swagger: `http://localhost:5040/swagger`
Health: `http://localhost:5040/health`

---

## Keycloak Setup

1. Create `CitizenRealm` in the existing Keycloak instance
2. Add client `citizen-portal-app` (public)
3. Configure GSIS/TaxisNet as OIDC Identity Provider
4. Create `citizen` realm role
5. Set first-login flow to auto-provision users
