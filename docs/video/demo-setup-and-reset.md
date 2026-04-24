# Demo Setup and Reset — IBM MQ Announcement Video

Three procedures: **initial setup**, **pre-recording strip-down of Acme.Billing**, and **reset between takes**.

---

## Procedure 1 — Initial setup (once before recording day)

Bring the full stack up and verify everything works end-to-end.

- [ ] **Step 1: Bring the stack up**

```bash
cd ~/src/ibmmq-bridge-demo
just build
just up
```

Wait ~60 seconds for all containers to settle.

- [ ] **Step 2: Verify containers are running**

```bash
podman compose ps
```

Expected: all services (`ibmmq`, `rabbitmq`, `ravendb`, `jaeger`, `sales`, `billing`, `dashboard`, `shipping`, `bridge`, three IBM MQ ServiceControl containers, three RabbitMQ ServiceControl containers, `servicepulse`, `servicepulse-rabbitmq`) show `Up`.

- [ ] **Step 3: Open all browser tabs**

| URL | Login | Purpose |
|-----|-------|---------|
| `https://localhost:9443` | `admin` / `passw0rd` | IBM MQ Web Console |
| `http://localhost:6001` | — | Acme.Dashboard |
| `http://localhost:9090` | — | ServicePulse (IBM MQ) |
| `http://localhost:16686` | — | Jaeger |

- [ ] **Step 4: Smoke-test the flow**

On the Dashboard, click "Place order". Verify:
- Order appears in the dashboard's live event stream.
- In ServicePulse, an audit entry appears under the auditing view.
- In Jaeger, a trace for `Acme.Sales` shows publish + receive spans.

If any of these fail, consult `CHALLENGES.md` in the repo root.

---

## Procedure 2 — Pre-recording strip-down of Acme.Billing

Take `src/Acme.Billing` back to scene 3's starting state. Keep the stripped-down version on a dedicated branch so `master` stays intact.

- [ ] **Step 1: Create a recording branch**

```bash
git checkout -b video-recording
```

- [ ] **Step 2: Strip `Program.cs` to the pre-baked OTel block only**

Overwrite `src/Acme.Billing/Program.cs` with ONLY the OTel configuration — no NServiceBus host setup yet. Use this content:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("Acme.Billing"))
            .WithTracing(t => t
                .SetSampler(new AlwaysOnSampler())
                .AddSource("NServiceBus.*")
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddMeter("NServiceBus.*")
                .AddOtlpExporter());
    })
    .Build();

await host.RunAsync();
```

- [ ] **Step 3: Delete the existing handlers**

```bash
rm src/Acme.Billing/OrderPlacedHandler.cs
rm src/Acme.Billing/OrderShippedHandler.cs
```

- [ ] **Step 4: Verify the project still builds**

```bash
dotnet build src/Acme.Billing/Acme.Billing.csproj
```

Expected: builds clean. If build fails with missing types (e.g., `IBMMQTransport`), that's expected — remove unused `using` directives until it builds. The file should only need `using` directives for OTel and hosting.

- [ ] **Step 5: Commit the stripped state**

```bash
git add src/Acme.Billing/Program.cs src/Acme.Billing/OrderPlacedHandler.cs src/Acme.Billing/OrderShippedHandler.cs
git commit -m "🎬 Strip Acme.Billing to scene 3 starting state for video recording"
```

Record scenes 3 and 4 from this state.

- [ ] **Step 6: Add Recoverability off-camera before scene 5**

After scene 4 is recorded and before scene 5, add the `Recoverability()` block to the `endpointConfiguration` in `Program.cs`:

```csharp
endpointConfiguration.Recoverability()
    .Immediate(i => i.NumberOfRetries(0))
    .Delayed(d => d.NumberOfRetries(0))
    .OnConsecutiveFailures(3, new RateLimitSettings(TimeSpan.FromSeconds(5)));
```

Stop the running endpoint before editing. Commit separately so the diff is visible if needed:

```bash
git add src/Acme.Billing/Program.cs
git commit -m "🎬 Add Recoverability for scene 5 (off-camera)"
```

---

## Procedure 3 — Reset between takes

Previous takes leave state that pollutes the recording: failed messages in ServicePulse, Jaeger traces from previous runs, queue depth on the `error` queue. Reset between retakes.

- [ ] **Step 1: Stop the Billing endpoint**

Stop the process in Rider (or `podman stop billing` if running containerized).

- [ ] **Step 2: Delete Billing's queues and subscription so installers recreate them**

```bash
podman exec ibmmq runmqsc QM1 <<'EOF'
DELETE SUB('Acme.Billing')
DELETE QLOCAL('Acme.Billing') PURGE
EOF
```

Expected: both commands succeed. If one fails because the object doesn't exist, ignore.

- [ ] **Step 3: Clear the error and audit queues**

```bash
podman exec ibmmq runmqsc QM1 <<'EOF'
CLEAR QLOCAL('error')
CLEAR QLOCAL('audit')
EOF
```

- [ ] **Step 4: Clear ServicePulse failed messages**

Open ServicePulse → Failed Messages → Group Actions → "Archive all groups". (ServicePulse doesn't expose a hard-delete in the UI; archiving removes them from the active view, which is sufficient for a clean recording.)

- [ ] **Step 5: Reset dashboard simulation settings for Billing**

On the Dashboard, open the Billing simulation panel. Set:
- Failure rate: 0%
- Processing delay: 0ms
- Concurrency: default (1 or whatever the repo default is)

- [ ] **Step 6: Verify clean state**

- IBM MQ Web Console: queue depth on `Acme.Billing`, `error`, `audit` all show `0`.
- ServicePulse → Failed Messages: empty / no groups.
- Jaeger: optional; older traces are harmless since you'll generate a fresh one in scene 6.

- [ ] **Step 7: Restart Billing to recreate its queue + subscription**

Start the endpoint from Rider. Wait for the startup banner. Confirm `Acme.Billing` queue and `DEV.ACME.ORDERPLACED` subscription are back in IBM MQ.

Ready for next take.

---

## After recording

Merge the `video-recording` branch changes back or drop the branch. Typical choice: drop the branch — the stripped state is only useful for re-recording.

```bash
git checkout master
git branch -D video-recording
```

If you want to keep the stripped state around for future videos, leave the branch and push it with a clear name.
