# NServiceBus IBM MQ / RabbitMQ Bridge Demo

Multi-transport [NServiceBus](https://particular.net/nservicebus) demo bridging IBM MQ and RabbitMQ via the [NServiceBus MessagingBridge](https://docs.particular.net/nservicebus/bridge/), with full [ServiceControl](https://docs.particular.net/servicecontrol/) observability, OpenTelemetry tracing, and a real-time web dashboard.

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
│  LegacySender ──EBCDIC──► Sales                       │
│    (raw 70-byte fixed-length records, no NServiceBus)  │
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
| **Acme.LegacySender** | IBM MQ | Simulates mainframe — sends raw 70-byte EBCDIC fixed-length records directly to MQ |
| **Acme.Bridge** | Both | NServiceBus MessagingBridge routing messages between transports |

### Message flow

**Commands**: `PlaceOrder`, `ShipOrder`, `UpdateSimulationSettings`
**Events**: `OrderPlaced`, `OrderShipped`
**Replies**: `OrderAccepted`, `ShipmentConfirmed`

### Legacy integration

The `LegacySender` simulates a mainframe system that writes raw EBCDIC (code page IBM500) records directly to IBM MQ — no NServiceBus headers, no JSON serialization. Each message is a 70-byte fixed-length record:

| Offset | Length | Format |
|--------|--------|--------|
| 0 | 36 | EBCDIC GUID string |
| 36 | 30 | EBCDIC product name (space-padded) |
| 66 | 4 | Big-endian Int32 quantity |

The `Sales` endpoint uses an `IMutateIncomingTransportMessages` pipeline mutator (`EbcdicMessageMutator`) to detect these raw messages (no `NServiceBus.EnclosedMessageTypes` header), decode the EBCDIC payload, and inject the proper NServiceBus headers so the message flows through the system like any other `PlaceOrder` command.

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
| `sc-ibmmq` | IBM MQ | Error instance |
| `sc-audit-ibmmq` | IBM MQ | Audit instance |
| `sc-mon-ibmmq` | IBM MQ | Monitoring instance |
| `sc-rabbitmq` | RabbitMQ | Error instance |
| `sc-audit-rabbitmq` | RabbitMQ | Audit instance |
| `sc-mon-rabbitmq` | RabbitMQ | Monitoring instance |

ServicePulse (port 9090) connects to the IBM MQ stack. A second ServicePulse instance (port 9091) connects to the RabbitMQ stack.

### Audit/error routing

- IBM MQ endpoints → `audit` / `error` queues on IBM MQ → bridged to RabbitMQ ServiceControl
- RabbitMQ endpoints → `audit.rabbitmq` / `error.rabbitmq` queues on RabbitMQ

## Prerequisites

- [Podman](https://podman.io/) with `podman-compose` (not Docker)
- [.NET SDK 10.0](https://dotnet.microsoft.com/)
- [`just`](https://just.systems/) task runner

**Linux note:** This stack runs many .NET processes that each use inotify for config reloading. You may need to raise the default limit:

```bash
sudo sysctl fs.inotify.max_user_instances=512
# Persist:
echo 'fs.inotify.max_user_instances=512' | sudo tee /etc/sysctl.d/99-inotify.conf
```

## Quick start

```bash
just build       # build container images + pull ServiceControl
just up          # start all services
just demo        # up + tmux monitor + open dashboard in browser
just logs        # follow all container logs
just sender      # run interactive CLI sender on host
just legacy-sender  # run EBCDIC legacy sender on host
just down        # stop everything
```

Run `just help` to see all available recipes.

## Web UIs

| URL | Service |
|-----|---------|
| http://localhost:6001 | Acme.Dashboard (order placement, simulation controls, live events) |
| http://localhost:9090 | ServicePulse — IBM MQ (heartbeats, failed messages, monitoring) |
| http://localhost:9091 | ServicePulse — RabbitMQ |
| http://localhost:16686 | Jaeger (distributed traces) |
| http://localhost:15672 | RabbitMQ Management (`guest` / `guest`) |
| https://localhost:9443 | IBM MQ Web Console (`admin` / `passw0rd`) |
| http://localhost:8181 | RavenDB Studio |

## IBM MQ configuration

Defined in `ibmmq-mqsc.ini`. Each service connects with its own MQ user and least-privilege authorization:

| MQ User | Endpoint | Channel |
|---------|----------|---------|
| `sales` | Acme.Sales | APP.SVRCONN |
| `billing` | Acme.Billing | APP.SVRCONN |
| `dashboard` | Acme.Dashboard | APP.SVRCONN |
| `bridge` | Acme.Bridge | APP.SVRCONN |
| `sender` | Acme.Sender | APP.SVRCONN |
| `fmainframe` | Acme.LegacySender | APP.SVRCONN |
| `admin` | ServiceControl | ADMIN.SVRCONN (mapped to `mqm`) |

**Pub/sub topics**: `DEV.ACME.ORDERPLACED`, `DEV.ACME.ORDERSHIPPED`

Connection auth is disabled (`CONNAUTH(' ')`) because rootless Podman's nosuid mounts prevent MQ's PAM-based password validation.

## Simulation

All endpoints support deterministic failure simulation controlled from the Dashboard UI:

- **Failure percentage** — counter-based (not random), e.g. 20% = every 5th message fails
- **Processing delay** — configurable per-endpoint artificial latency
- **Concurrency** — adjustable message processing concurrency
- **Transaction mode** — switchable between `SendsAtomicWithReceive`, `ReceiveOnly`, and `None`

Settings are sent as `UpdateSimulationSettings` commands to per-endpoint `.control` queues and persisted to disk. Concurrency and transaction mode changes trigger a graceful endpoint restart.

Recoverability is configured for demo purposes: immediate and delayed retries are disabled, with rate-limiting at 5-second intervals after 3 consecutive failures.

## Observability

- **OpenTelemetry** — `AlwaysOnSampler` on all endpoints, NServiceBus activity sources, OTLP gRPC export to Jaeger
- **ServiceControl** — heartbeats, audit/error ingestion, monitoring metrics at 500ms intervals
- **Jaeger** — collects distributed traces via OTLP on port 4317

Note: The MessagingBridge does not emit OpenTelemetry traces or metrics (not supported in the current version).

## Project structure

```
├── docker-compose.yml              # All services (infra, apps, ServiceControl)
├── justfile                        # Task runner recipes
├── ibmmq.Dockerfile                # IBM MQ server image
├── ibmmq-mqsc.ini                  # Queue/channel/topic/auth definitions
├── ibmmq-mqwebuser.xml             # MQ web console credentials
├── rabbitmq.conf                   # RabbitMQ config
├── servicecontrol-transports/      # SC IBM MQ transport DLLs (bind-mounted)
├── CHALLENGES.md                   # Known issues and resolutions
├── src/
│   ├── Acme.Sales/                 # Order intake (IBM MQ)
│   ├── Acme.Billing/               # Invoice processing (IBM MQ)
│   ├── Acme.Dashboard/             # Web UI + SSE events (IBM MQ)
│   ├── Acme.Shipping/              # Fulfillment (RabbitMQ)
│   ├── Acme.Bridge/                # Cross-transport bridge
│   ├── Acme.Sender/                # CLI order sender
│   ├── Acme.LegacySender/          # EBCDIC mainframe simulator
│   └── Acme.Shared/                # Message contracts + simulation framework
```

## Known challenges

See [CHALLENGES.md](CHALLENGES.md) for detailed write-ups of issues encountered and their resolutions, including:

1. Bridge endpoint registration semantics (`HasEndpoint` creates proxy on the *other* transport)
2. ServiceControl Audit `RegisterNewEndpoint` routing
3. Podman + SELinux volume mount permissions
4. RabbitMQ Erlang cookie under rootless Podman
5. OpenTelemetry parent-based sampling dropping traces
6. MessagingBridge lacking OpenTelemetry support
7. IBM MQ `dspmqver` command not found (harmless)
8. IBM MQ `MQ_ADMIN_PASSWORD` deprecation warning

## License

[MIT](LICENSE)
