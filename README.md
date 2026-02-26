# IBM MQ Bridge Demo

Multi-transport NServiceBus demo bridging IBM MQ and RabbitMQ with full ServiceControl observability, OpenTelemetry tracing, and a real-time web dashboard.

## Architecture

```
┌─ IBM MQ ──────────────────────────────────────────────┐
│                                                        │
│  Sender ──PlaceOrder──► Sales ──ShipOrder──►(bridge)  │
│    ▲                      │                            │
│    └─OrderAccepted────────┘                            │
│                           ├─publish: OrderPlaced       │
│                           │                            │
│  Dashboard (web UI) ◄─────┤                            │
│    ▲ SSE event stream     │                            │
│    │                      │                            │
│  Billing ◄────────────────┘                            │
│    (subscribes OrderPlaced + OrderShipped via bridge)   │
│                                                        │
└──────────────────────┬─────────────────────────────────┘
                       │
                 ┌─────┴─────┐
                 │  Bridge   │
                 └─────┬─────┘
                       │
┌──────────────────────┴─────────────────────────────────┐
│                                                        │
│  Shipping ◄─ShipOrder                                  │
│    ├─reply:   ShipmentConfirmed ──► Sales              │
│    └─publish: OrderShipped ──► Dashboard, Billing      │
│                                                        │
└─ RabbitMQ ─────────────────────────────────────────────┘
```

### Endpoints

| Endpoint | Transport | Role |
|----------|-----------|------|
| **Acme.Sales** | IBM MQ | Receives `PlaceOrder`, publishes `OrderPlaced`, sends `ShipOrder` to Shipping |
| **Acme.Billing** | IBM MQ | Subscribes to `OrderPlaced` and `OrderShipped` for invoicing |
| **Acme.Dashboard** | IBM MQ | ASP.NET web UI with SSE event stream, simulation controls, order placement |
| **Acme.Shipping** | RabbitMQ | Handles `ShipOrder`, publishes `OrderShipped`, replies `ShipmentConfirmed` |
| **Acme.Sender** | IBM MQ | Interactive CLI for manual order placement |
| **Acme.Bridge** | Both | NServiceBus MessagingBridge routing messages between transports |

### Message types

**Commands**: `PlaceOrder`, `ShipOrder`, `UpdateSimulationSettings`
**Events**: `OrderPlaced`, `OrderShipped`
**Replies**: `OrderAccepted`, `ShipmentConfirmed`

## Infrastructure

| Service | Container | Port | Purpose |
|---------|-----------|------|---------|
| IBM MQ | `ibmmq` | 1414 (MQ), 9443 (web console) | Queue manager QM1 |
| RabbitMQ | `rabbitmq` | 15672 (management) | Quorum queues |
| RavenDB | `ravendb` | 8181 | ServiceControl persistence |
| Jaeger | `jaeger` | 16686 (UI), 4317 (OTLP) | Distributed tracing |

### ServiceControl

Two independent ServiceControl stacks, one per transport:

| Container | Transport | Role |
|-----------|-----------|------|
| `sc-ibmmq` | IBM MQ | Error instance (port 33333) |
| `sc-audit-ibmmq` | IBM MQ | Audit instance (port 44444) |
| `sc-mon-ibmmq` | IBM MQ | Monitoring instance (port 33633) |
| `sc-rabbitmq` | RabbitMQ | Error instance |
| `sc-audit-rabbitmq` | RabbitMQ | Audit instance |
| `sc-mon-rabbitmq` | RabbitMQ | Monitoring instance |

ServicePulse (`servicepulse`, port 9090) connects to the IBM MQ ServiceControl stack. It only supports a single monitoring URL.

### Audit/error routing

- IBM MQ endpoints → `audit` / `error` queues on IBM MQ
- RabbitMQ endpoints → `audit.rabbitmq` / `error.rabbitmq` queues on RabbitMQ

## Prerequisites

- Podman (not Docker)
- .NET SDK 10.0
- `just` task runner

## Quick start

```bash
just up          # build containers + start everything, opens dashboard
just logs        # follow all container logs
just monitor     # tmux split view of endpoint logs
just sender      # run interactive CLI sender on host
just down        # stop everything
```

## Web UIs

| URL | Service |
|-----|---------|
| http://localhost:6001 | Acme.Dashboard (order placement, simulation controls, live events) |
| http://localhost:9090 | ServicePulse (heartbeats, failed messages, monitoring) |
| http://localhost:16686 | Jaeger (distributed traces) |
| http://localhost:15672 | RabbitMQ Management (guest/guest) |
| https://localhost:9443 | IBM MQ Web Console (admin/passw0rd) |
| http://localhost:8181 | RavenDB Studio |

## IBM MQ configuration

Defined in `ibmmq-mqsc.ini`. Per-service users with least-privilege auth:

| MQ User | Endpoint | Channel |
|---------|----------|---------|
| `sales` | Acme.Sales | APP.SVRCONN |
| `billing` | Acme.Billing | APP.SVRCONN |
| `dashboard` | Acme.Dashboard | APP.SVRCONN |
| `bridge` | Acme.Bridge | APP.SVRCONN |
| `sender` | Acme.Sender | APP.SVRCONN |
| `admin` | ServiceControl | ADMIN.SVRCONN (mapped to mqm) |

Pub/sub topics: `DEV.ACME.ORDERPLACED`, `DEV.ACME.ORDERSHIPPED`

Connection auth is disabled (`CONNAUTH(' ')`) because rootless Podman's nosuid mounts prevent MQ's PAM-based password validation.

## Simulation

All endpoints support deterministic failure simulation controlled from the Dashboard UI:

- **Failure percentage**: counter-based (not random) — e.g., 20% means every 5th message fails
- **Processing delay**: configurable per-endpoint
- **Concurrency**: adjustable via `UpdateSimulationSettings` command to `.control` queues

Recoverability: immediate and delayed retries disabled; rate-limiting at 5-second intervals after 3 failures.

## Observability

- **OpenTelemetry**: `AlwaysOnSampler` on all endpoints, metrics at 500ms intervals
- **ServiceControl**: heartbeats, custom checks, audit/error ingestion
- **Jaeger**: collects traces via OTLP (gRPC on port 4317)

## Project structure

```
├── docker-compose.yml              # All services
├── justfile                        # Task runner recipes
├── ibmmq.Dockerfile                # IBM MQ server image
├── ibmmq-mqsc.ini                  # Queue/channel/auth definitions
├── ibmmq-mqwebuser.xml             # MQ web console config
├── rabbitmq.conf                   # RabbitMQ config (OAuth disabled)
├── servicecontrol-transports/IBMMQ # SC IBM MQ transport DLLs
├── CHALLENGES.md                   # Known issues and resolutions
├── src/
│   ├── Acme.Sales/                 # Order intake (IBM MQ)
│   ├── Acme.Billing/               # Invoice processing (IBM MQ)
│   ├── Acme.Dashboard/             # Web UI + SSE events (IBM MQ)
│   ├── Acme.Shipping/              # Fulfillment (RabbitMQ)
│   ├── Acme.Bridge/                # Cross-transport bridge
│   ├── Acme.Sender/                # CLI order sender (IBM MQ)
│   └── Acme.Shared/                # Message contracts + simulation
```
