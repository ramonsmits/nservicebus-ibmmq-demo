# Challenges & Resolutions

## 1. Bridge endpoint registration semantics

**Problem:** Audit and error messages from IBM MQ endpoints (Sales, Dashboard, Billing) were not reaching ServiceControl on RabbitMQ. Messages sent to the `audit` and `error` queues stayed on IBM MQ with no bridge proxy to forward them.

**Root cause:** `ibmMq.HasEndpoint("audit")` creates a proxy for `audit` on the RabbitMQ side, meaning it expects `audit` to live on IBM MQ. But `audit` and `error` queues live on RabbitMQ (where ServiceControl listens). The semantics are: `transport.HasEndpoint("X")` means endpoint X **lives on** that transport, and the bridge creates a proxy on the **other** transport.

**Fix:** Moved `audit` and `error` from `ibmMq.HasEndpoint(...)` to `rabbitMq.HasEndpoint(...)` in `src/Acme.Bridge/Program.cs`. This creates IBM MQ proxy queues that forward to the real RabbitMQ queues.

## 2. ServiceControl Audit RegisterNewEndpoint routing failure

**Problem:** ServiceControl Audit logged errors: `No destination could be found for message type RegisterNewEndpoint`. When a new endpoint sends its first audit message, SC Audit sends a `RegisterNewEndpoint` command to the main ServiceControl instance, but didn't know its queue address.

**Root cause:** The ServiceControl Audit container was missing the `ServiceControl__ServiceControlQueueAddress` environment variable, so it couldn't route `RegisterNewEndpoint` commands to the main ServiceControl instance.

**Fix:** Added `ServiceControl__ServiceControlQueueAddress: Particular.ServiceControl` to the `servicecontrol-audit` service in `docker-compose.yml`.

## 3. Podman + SELinux volume mount permissions

**Problem:** Containers running under rootless Podman on Fedora with SELinux enforcing got `Permission denied` when accessing bind-mounted volumes, even when Unix permissions were correct.

**Root cause:** SELinux labels on host files don't match what the container expects. Podman volume mounts require `:z` (shared) or `:Z` (private) suffix for SELinux relabeling, or `security_opt: [label=disable]` to skip confinement.

**Fix:** Added `security_opt: [label=disable]` to containers with bind mounts or complex filesystem access (IBM MQ, RabbitMQ, Jaeger, RavenDB).

## 4. RabbitMQ Erlang cookie failure under rootless Podman

**Problem:** RabbitMQ container failed to start with Erlang cookie permission errors. Podman-compose creates `.erlang.cookie` with root ownership; Erlang can't recreate it under rootless Podman's umask.

**Fix:** Custom entrypoint that removes the stale cookie and recreates it with correct ownership before starting RabbitMQ:
```yaml
entrypoint: ["/bin/bash", "-c", "rm -f /var/lib/rabbitmq/.erlang.cookie && gosu rabbitmq bash -c 'openssl rand -hex 16 > /var/lib/rabbitmq/.erlang.cookie && chmod 400 /var/lib/rabbitmq/.erlang.cookie' && exec docker-entrypoint.sh rabbitmq-server"]
```

## 5. OpenTelemetry traces missing for Sales (and Bridge)

**Problem:** After enabling OpenTelemetry on all endpoints, Jaeger showed traces from Dashboard, Billing, and Shipping — but not from Sales. All endpoints used identical OTel configuration with `.AddSource("NServiceBus.Core")`.

**Debugging journey:**

1. Verified OTel DLLs were present in the Sales container
2. Verified DNS resolution and `OTEL_EXPORTER_OTLP_ENDPOINT` env var were correct
3. Added `OpenTelemetry.Exporter.Console` to Sales — **zero output**, confirming traces weren't being collected at all
4. Used reflection to confirm the ActivitySource name is exactly `NServiceBus.Core` (version `0.1.0`) — the name was correct
5. Added a custom `ActivitySource("Acme.Sales.Diag")` with a hosted service that creates a test activity on startup — **this worked**, proving the OTel SDK was functioning
6. Added a raw `System.Diagnostics.ActivityListener` that listens to `NServiceBus.Core` — **caught all activities**, proving NServiceBus was creating them
7. With the raw listener active (forcing `AllDataAndRecorded` sampling), the OTel console exporter started showing NServiceBus traces — confirming the issue was **sampling**

**Root cause:** The default OTel sampler is `ParentBasedSampler(AlwaysOnSampler)`. This sampler respects the parent trace context: if the parent span has `TraceFlags = 00` (not recorded), child spans are also dropped. Messages arriving at Sales directly via IBM MQ from Dashboard carried trace parent headers with unsampled flags, causing the `ParentBasedSampler` to drop all NServiceBus activities.

Billing worked because its messages arrived through the Bridge, which re-created trace context during forwarding.

**Fix:** Added `SetSampler(new AlwaysOnSampler())` to all endpoints:
```csharp
.WithTracing(t => t
    .SetSampler(new AlwaysOnSampler())
    .AddSource("NServiceBus.*")
    .AddOtlpExporter())
```

Also switched from exact `AddSource("NServiceBus.Core")` to wildcard `AddSource("NServiceBus.*")` per Particular docs recommendation.

## 6. MessagingBridge has no OpenTelemetry support

**Problem:** After fixing Sales, the Bridge still didn't appear in Jaeger despite having identical OTel configuration.

**Root cause:** The `NServiceBus.MessagingBridge` 5.0.1 package contains no `System.Diagnostics.ActivitySource`. It doesn't emit traces or metrics. Confirmed via reflection — zero ActivitySource fields in the assembly.

**Fix:** Removed OTel packages, configuration, and `OTEL_EXPORTER_OTLP_ENDPOINT` env var from the Bridge since they were dead weight.

## 7. IBM MQ `dspmqver` command not found

**Problem:** All IBM MQ endpoint containers log `bash: line 1: dspmqver: command not found` on startup.

**Root cause:** The IBM MQ client library attempts to run `dspmqver` (a server-side diagnostic tool) to detect the MQ version. This tool is only available in the full MQ server image, not in the client libraries used by the application containers.

**Impact:** Harmless — the endpoints function correctly without it. The IBM MQ .NET client falls back gracefully.

## 8. IBM MQ `MQ_ADMIN_PASSWORD` deprecation

**Problem:** The IBM MQ container logs warnings about `MQ_ADMIN_PASSWORD` being deprecated.

**Impact:** Cosmetic only. The current `MQ_ADMIN_PASSWORD` env var still works but will eventually be replaced in newer IBM MQ container images.
