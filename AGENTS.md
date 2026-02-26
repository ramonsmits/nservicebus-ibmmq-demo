# AGENTS.md

## Project overview

Multi-transport NServiceBus demo: IBM MQ endpoints bridged to RabbitMQ via NServiceBus.MessagingBridge, with dual ServiceControl stacks and a web dashboard.

## Build & run

```bash
just up        # build + start all containers (podman compose)
just down      # stop
just logs      # tail all logs
just sender    # run CLI sender on host
just dotnet-build  # build .NET solution only
```

Requires **Podman** (not Docker), **.NET SDK 10.0**, and `just`.

## Container runtime

This project uses **Podman** with `podman compose`. All `docker-compose.yml` services have explicit `container_name` values (no `-1` suffixes). Use `podman` commands, not `docker`.

SELinux is enforcing — bind mounts use `:z` suffix or `security_opt: [label=disable]`.

## Service naming conventions

- IBM MQ ServiceControl: `sc-ibmmq`, `sc-audit-ibmmq`, `sc-mon-ibmmq`
- RabbitMQ ServiceControl: `sc-rabbitmq`, `sc-audit-rabbitmq`, `sc-mon-rabbitmq`
- Application endpoints use their short name: `sales`, `billing`, `dashboard`, `shipping`, `bridge`
- Infrastructure: `ibmmq`, `rabbitmq`, `ravendb`, `jaeger`, `servicepulse`

When referencing containers in compose config (depends_on, URLs), use the **service name** which doubles as the DNS hostname in the compose network.

## Transport topology

- **IBM MQ endpoints**: Acme.Sales, Acme.Billing, Acme.Dashboard, Acme.Sender
- **RabbitMQ endpoints**: Acme.Shipping
- **Bridge**: routes messages between transports; has no OpenTelemetry support (MessagingBridge doesn't emit traces)

Audit/error queues are split per transport:
- IBM MQ side: `audit`, `error`
- RabbitMQ side: `audit.rabbitmq`, `error.rabbitmq`

## ServicePulse configuration

ServicePulse uses YARP reverse proxy. Key env vars:
- `SERVICECONTROL_URL` — single SC error instance URL (required, with `/api` path)
- `MONITORING_URL` — single SC monitoring instance URL (singular, not `MONITORING_URLS`)
- `MONITORING_URLS` (legacy) — expects a **JSON array** string, not comma-separated. Only the first entry is used.

ServicePulse only supports **one** monitoring URL. For the second transport stack, a separate ServicePulse instance would be needed.

## IBM MQ specifics

- Queue manager: `QM1`, port 1414
- Channels: `ADMIN.SVRCONN` (admin/ServiceControl), `APP.SVRCONN` (application endpoints)
- Connection auth is disabled (rootless Podman nosuid prevents PAM auth)
- Per-service users with least-privilege authorization defined in `ibmmq-mqsc.ini`
- Topic naming: `DEV.{NAMESPACE}.{CLASSNAME}` / `dev/{namespace}.{classname}/`
- ServiceControl transport DLLs are bind-mounted from `servicecontrol-transports/IBMMQ/`

## ServiceControl images

Currently using pre-release images from `ghcr.io/particular/servicecontrol:pr-5350` (IBM MQ transport support). These are not stable releases.

## Key files

| File | Purpose |
|------|---------|
| `docker-compose.yml` | All services, networking, volumes |
| `justfile` | Build/run recipes |
| `ibmmq-mqsc.ini` | MQ queues, channels, topics, auth records |
| `ibmmq-mqwebuser.xml` | MQ web console user config |
| `rabbitmq.conf` | RabbitMQ settings (disables OAuth) |
| `CHALLENGES.md` | Known issues and their resolutions |
| `src/Acme.Bridge/Program.cs` | Bridge configuration (endpoint registrations, publisher mappings) |
| `src/Acme.Shared/Messages/` | All message contracts (commands, events, replies) |

## Common pitfalls

- The `justfile` `monitor` recipe references container names — update if container names change
- ServiceControl images require the IBM MQ transport DLLs mounted at `/app/Transports/IBMMQ`
- RabbitMQ needs a custom entrypoint to fix Erlang cookie permissions under rootless Podman
- `dspmqver: command not found` in endpoint logs is harmless (MQ client probes for server tool)
