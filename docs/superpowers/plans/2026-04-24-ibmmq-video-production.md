# IBM MQ Transport Announcement Video — Production Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce the content artifacts (presenter script, slide copy, reference source code, demo setup/reset procedures) needed to record the ~6-minute IBM MQ transport announcement video described in `docs/superpowers/specs/2026-04-24-ibmmq-video-design.md`.

**Architecture:** This plan produces content, not code. Each task creates a file under `docs/video/`. The recording itself, the actual slide deck rendering (Keynote/PowerPoint), and post-production editing are the presenter's and editor's craft — this plan supplies the raw material they need.

**Tech Stack:** Markdown for all content artifacts; the reference source files use the project's existing C# conventions (.NET 10, NServiceBus 10.x, NServiceBus.Transport.IBMMQ 1.x).

**Out of scope:** Recording mechanics, video editing specifics, slide deck visual design. The demo stack already exists and is not modified by this plan.

---

## File structure

```
docs/
├── superpowers/
│   ├── specs/2026-04-24-ibmmq-video-design.md      (design spec — already exists)
│   └── plans/2026-04-24-ibmmq-video-production.md  (this plan)
└── video/
    ├── README.md                            (folder index)
    ├── presenter-script.md                  (VO + beats per scene — primary artifact)
    ├── slides.md                            (slide copy for scenes 1, 2, 7)
    ├── demo-setup-and-reset.md              (demo state setup + reset-between-takes checklist)
    └── reference/
        ├── Program.cs                       (final-state Acme.Billing/Program.cs)
        └── OrderPlacedHandler.cs            (final-state handler)
```

Each file has one responsibility and is consumed by exactly one audience (presenter vs. editor vs. demo-operator).

---

## Task 1: Scaffold the `docs/video/` folder

**Files:**
- Create: `docs/video/README.md`

**Purpose:** Single entry point that tells the presenter, editor, and demo operator where to find each artifact and in what order to use them.

- [ ] **Step 1: Create the README**

```markdown
# IBM MQ Announcement Video — Production Artifacts

Supporting material for the ~6-minute announcement video described in
[`../superpowers/specs/2026-04-24-ibmmq-video-design.md`](../superpowers/specs/2026-04-24-ibmmq-video-design.md).

## What's in this folder

| File | Purpose | Audience |
|------|---------|----------|
| [`presenter-script.md`](presenter-script.md) | Scene-by-scene voiceover, beats, and on-screen actions. Read top to bottom during recording. | Presenter |
| [`slides.md`](slides.md) | Slide copy for scenes 1 (title), 2 (agenda), 7 (outro). Hand off to whoever renders the deck. | Editor / slide designer |
| [`demo-setup-and-reset.md`](demo-setup-and-reset.md) | Commands and checklists to bring the demo stack into a clean recording state and reset it between takes. | Demo operator (usually also the presenter) |
| [`reference/Program.cs`](reference/Program.cs) | Final-state `Acme.Billing/Program.cs` — what the presenter's typing should converge to. | Presenter |
| [`reference/OrderPlacedHandler.cs`](reference/OrderPlacedHandler.cs) | Final-state handler file. | Presenter |

## Workflow

1. Read the design spec (`../superpowers/specs/2026-04-24-ibmmq-video-design.md`) — understand the why.
2. Run through `demo-setup-and-reset.md` to bring the stack up clean.
3. Strip `src/Acme.Billing/` to scene 3's starting state per `demo-setup-and-reset.md`.
4. Open `reference/Program.cs` and `reference/OrderPlacedHandler.cs` on a second monitor.
5. Record scenes 3–6 following `presenter-script.md`. Retry takes freely — the video is edited.
6. Send recordings + `slides.md` + `presenter-script.md` (for VO reference) to the editor.
```

- [ ] **Step 2: Verify the file**

Run: `ls -la docs/video/README.md && head -5 docs/video/README.md`
Expected: file exists, header reads `# IBM MQ Announcement Video — Production Artifacts`.

- [ ] **Step 3: Commit**

```bash
git add docs/video/README.md
git commit -m "📝 Add video/ folder index for IBM MQ announcement video"
```

---

## Task 2: Write final-state `reference/Program.cs`

**Files:**
- Create: `docs/video/reference/Program.cs`

**Purpose:** The target state the presenter's live typing converges on by end of scene 5 (after the 4 platform-wiring lines are added). The presenter keeps this visible on a second monitor while recording.

This matches the existing `src/Acme.Billing/Program.cs` but with the demo-specific noise stripped (no `EbcdicMessageMutator`, no `RandomFailureBehavior` at the code level — the dashboard simulation controls handle failure injection externally, no `CustomDiagnosticsWriter`). It still includes OTel (pre-baked, shown but not narrated) and Recoverability (added off-camera between scenes 4 and 5).

- [ ] **Step 1: Write the file**

```csharp
// Final-state reference for Acme.Billing/Program.cs
// Matches what the presenter types by end of scene 5.
// - OpenTelemetry block at top: pre-baked, visible in scene 3 but not narrated.
// - NServiceBus block: typed live in scene 3 (transport + handler) and scene 5 (4 platform lines).
// - Recoverability block: added off-camera between scenes 4 and 5.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Transport.IBMMQ;
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
    .UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("Acme.Billing");

        var transport = new IBMMQTransport
        {
            Host = "ibmmq",
            Port = 1414,
            QueueManagerName = "QM1",
            Channel = "APP.SVRCONN",
            User = "billing",
        };

        endpointConfiguration.UseTransport(transport);
        endpointConfiguration.EnableInstallers();

        endpointConfiguration.Recoverability()
            .Immediate(i => i.NumberOfRetries(0))
            .Delayed(d => d.NumberOfRetries(0))
            .OnConsecutiveFailures(3, new RateLimitSettings(TimeSpan.FromSeconds(5)));

        endpointConfiguration.AuditProcessedMessagesTo("audit");
        endpointConfiguration.SendFailedMessagesTo("error");
        endpointConfiguration.SendHeartbeatTo("Particular.ServiceControl");
        endpointConfiguration
            .EnableMetrics()
            .SendMetricDataToServiceControl("Particular.Monitoring", TimeSpan.FromSeconds(10));

        return endpointConfiguration;
    })
    .Build();

await host.RunAsync();
```

- [ ] **Step 2: Verify the file compiles against the solution**

Because this is a reference file, it won't be in the build. Instead, spot-check the syntax by opening it in Rider and confirming no red squiggles when temporarily dropped into `src/Acme.Billing/Program.cs`.

Run: `test -f docs/video/reference/Program.cs && wc -l docs/video/reference/Program.cs`
Expected: file exists, roughly 45–55 lines.

- [ ] **Step 3: Commit**

```bash
git add docs/video/reference/Program.cs
git commit -m "📝 Add final-state Program.cs reference for video scene 3+5"
```

---

## Task 3: Write final-state `reference/OrderPlacedHandler.cs`

**Files:**
- Create: `docs/video/reference/OrderPlacedHandler.cs`

**Purpose:** The handler file as the presenter writes it during scene 3 beat 5. Simplest possible — log the message, return `Task.CompletedTask`.

- [ ] **Step 1: Write the file**

```csharp
// Final-state reference for Acme.Billing/OrderPlacedHandler.cs
// Typed live during scene 3 beat 5.

using Microsoft.Extensions.Logging;
using NServiceBus;

namespace Acme;

sealed class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger)
    : IHandleMessages<OrderPlaced>
{
    public Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        logger.LogInformation(
            "Received OrderPlaced {{ OrderId = {OrderId}, Product = {Product}, Quantity = {Quantity} }}",
            message.OrderId, message.Product, message.Quantity);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Verify the file**

Run: `test -f docs/video/reference/OrderPlacedHandler.cs && wc -l docs/video/reference/OrderPlacedHandler.cs`
Expected: file exists, roughly 15–20 lines.

- [ ] **Step 3: Commit**

```bash
git add docs/video/reference/OrderPlacedHandler.cs
git commit -m "📝 Add final-state OrderPlacedHandler reference for video scene 3"
```

---

## Task 4: Write `presenter-script.md`

**Files:**
- Create: `docs/video/presenter-script.md`

**Purpose:** One document, read top to bottom during recording. For each scene: starting state, what to do on screen, exact VO lines, callouts to flag for the editor. Timings are targets; recording is edited so slight overshoot is fine.

- [ ] **Step 1: Write the file**

````markdown
# Presenter Script — IBM MQ Transport Announcement Video

**Target total length:** ~6:00. Recording is edited — take as many attempts per scene as needed.

**Reference files to keep open on second monitor:**
- `reference/Program.cs`
- `reference/OrderPlacedHandler.cs`

**Tabs open in browser before recording:**
- IBM MQ Web Console — `https://localhost:9443` (logged in as `admin`)
- Acme.Dashboard — `http://localhost:6001`
- ServicePulse (IBM MQ) — `http://localhost:9090`
- Jaeger — `http://localhost:16686`

---

## Scene 1 — Title card (0:00–0:15) — added in post

**No recording needed.** Editor generates this slide. VO recorded separately:

> **VO:** "The NServiceBus IBM MQ transport is generally available."

---

## Scene 2 — Agenda (0:15–0:30) — added in post

**No recording needed.** Editor generates this slide. VO recorded separately:

> **VO:** "In the next five minutes: build a subscriber from scratch, see how it shows up in IBM MQ, handle a failure, and trace it end-to-end."

---

## Scene 3 — Build a subscriber live (0:30–2:00)

**Starting state:**
- Acme.Billing stopped.
- `src/Acme.Billing/Program.cs` contains ONLY the pre-baked OTel block (see `demo-setup-and-reset.md` for setup).
- `src/Acme.Billing/OrderPlacedHandler.cs` does not exist.
- Rider is showing the Acme.Billing project in the solution tree, `Program.cs` open.

### Beat 1 — project intro (~5s)

**Do:** Make sure Program.cs (with OTel block) and the .csproj tab are visible. Briefly click on the .csproj tab to show package references, then return to Program.cs.

**VO:** "Empty console app. Message contracts already referenced. Standard OTel wiring is already here — not the point today."

### Beat 2 — show packages (~5s)

**Do:** Briefly highlight `NServiceBus` and `NServiceBus.Transport.IBMMQ` package references in the .csproj.

**VO:** "Transport ships on public NuGet."

### Beat 3 — endpoint config (~25s)

**Do:** Below the OTel block, type the NServiceBus host + transport configuration. Target:

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(/* (existing OTel block stays here) */)
    .UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("Acme.Billing");

        var transport = new IBMMQTransport
        {
            Host = "ibmmq",
            Port = 1414,
            QueueManagerName = "QM1",
            Channel = "APP.SVRCONN",
            User = "billing",
        };

        endpointConfiguration.UseTransport(transport);
        endpointConfiguration.EnableInstallers();

        return endpointConfiguration;
    })
    .Build();

await host.RunAsync();
```

**VO:** "Name the endpoint. Wire up the transport — the same connection values any MQ client uses: host, port, queue manager, channel, user. `EnableInstallers` auto-provisions queues and topic subscriptions in dev."

### Beat 4 — consistency beat (~10s)

**Do:** Cursor hovering on the transport block; pause for emphasis.

**VO:** "Transport is transactional by default. Message receive and outgoing sends commit atomically. Handler throws — message rolls back to the queue. No custom transaction code."

**[Editor note]** Consider a subtle text overlay during this beat: "Transactional receive + send" / "At-least-once, idempotent-safe".

### Beat 5 — handler (~20s)

**Do:** Create a new file `OrderPlacedHandler.cs` in the Billing project and type:

```csharp
using Microsoft.Extensions.Logging;
using NServiceBus;

namespace Acme;

sealed class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger)
    : IHandleMessages<OrderPlaced>
{
    public Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        logger.LogInformation(
            "Received OrderPlaced {{ OrderId = {OrderId}, Product = {Product}, Quantity = {Quantity} }}",
            message.OrderId, message.Product, message.Quantity);
        return Task.CompletedTask;
    }
}
```

**VO:** "One handler class. Strongly-typed contract. Log it."

### Beat 6 — stability beat (~10s)

**Do:** Fit all of Program.cs on screen. Pause so the viewer can see it in one take.

**VO:** "That's the endpoint. Fifteen lines. This programming model has been in production for more than fifteen years."

### Beat 7 — run (~5s)

**Do:** Finger hovers over the run/debug button in Rider.

**VO:** "Let's run it."

**[Editor note]** Cut to scene 4 immediately as the run button is clicked.

---

## Scene 4 — Queues + topic subscription in IBM MQ (2:00–2:45)

**Starting state:** Acme.Billing starting (run from scene 3). MQ Web Console tab ready but not focused. Dashboard tab ready.

### Beat 1 — endpoint starts (~5s)

**Do:** Show Rider console with NServiceBus startup banner.

**VO:** "Endpoint starts up, opens a channel to QM1."

### Beat 2 — queue appeared (~10s)

**Do:** Switch to IBM MQ Web Console → Queue Manager QM1 → Queues. Find `Acme.Billing` in the list.

**VO:** "There's my endpoint queue — NServiceBus created it on startup because I enabled installers."

**[Editor note]** Red outline around the `Acme.Billing` queue row.

### Beat 3 — topic subscription appeared (~15s)

**Do:** Navigate to Subscriptions view. Find the subscription routing `DEV.ACME.ORDERPLACED` → `Acme.Billing`.

**VO:** "Under subscriptions — a subscription on `DEV.ACME.ORDERPLACED`, routing to my queue. Native IBM MQ pub/sub. No proxy, no shadow scheme — NServiceBus wires up real MQ primitives."

**[Editor note]** Red outline on the subscription row.

### Beat 4 — publish order (~10s)

**Do:** Switch to Dashboard tab. Click "Place order".

**VO:** "Acme.Sales is the publisher — already running. Place an order from the dashboard."

### Beat 5 — handler fires (~5s)

**Do:** Switch back to Rider console. Show the log line `Received OrderPlaced { OrderId = …, Product = …, Quantity = … }`.

**VO:** "Handler fires. End-to-end pub/sub over IBM MQ."

**[Editor note]** Arrow from "Place order" click → Rider log line.

---

## Scene 5 — Failure, error queue, replay (2:45–4:30)

**Starting state:** Acme.Billing running. Recoverability block already present in `Program.cs` (added off-camera between scenes — see `demo-setup-and-reset.md`).

### Beat 1 — setup for platform wiring (~8s)

**Do:** Return to Rider, cursor below the transport/handler setup.

**VO:** "So far the endpoint runs, but the platform doesn't know about it. Let me wire it in."

### Beat 2 — type four platform lines (~20s)

**Do:** Type these four lines:

```csharp
endpointConfiguration.AuditProcessedMessagesTo("audit");
endpointConfiguration.SendFailedMessagesTo("error");
endpointConfiguration.SendHeartbeatTo("Particular.ServiceControl");
endpointConfiguration
    .EnableMetrics()
    .SendMetricDataToServiceControl("Particular.Monitoring", TimeSpan.FromSeconds(10));
```

**VO:** "Audit — successful messages are copied to an audit queue. Error — failures go here. Heartbeat — so the platform knows I'm alive. Metrics — throughput, retry rates, processing time. Four lines, opt-in."

**[Editor note]** Highlight each line as it's typed.

### Beat 3 — recoverability aside (~8s)

**Do:** Scroll up to the pre-existing `Recoverability()` block and briefly highlight it.

**VO:** "Retries are configured here — three attempts, then the message is considered failed."

### Beat 4 — restart (~5s)

**Do:** Stop + start the endpoint.

**VO:** "Restart."

### Beat 5 — dial failure to 100% (~8s)

**Do:** Switch to Dashboard → simulation panel for Billing → set failure rate to 100%.

**VO:** "On the dashboard, set this endpoint's failure rate to a hundred percent."

### Beat 6 — place order, watch it fail (~12s)

**Do:** Click "Place order". Switch to Rider console and let viewer see retry attempts + final exception lines.

**VO:** "Place an order. Retries happen. All three fail."

**[Editor note]** Underline retry log lines in the Rider console.

### Beat 7 — message in error queue (~8s)

**Do:** Switch to MQ Web Console → `error` queue (depth = 1). Click the message to expand and show headers.

**VO:** "Message on the `error` queue. All NServiceBus headers preserved, full stack trace."

**[Editor note]** Zoom into the message headers panel.

### Beat 8 — ServicePulse failed message (~15s)

**Do:** Switch to ServicePulse → Failed Messages → find the group for `OrderPlacedHandler`. Click into it — show exception details, headers, body.

**VO:** "In ServicePulse, failures group by root cause. Click in — full exception, headers, body, everything."

### Beat 9 — fix + replay (~15s)

**Do:** Switch to Dashboard, set Billing failure rate back to 0%. Switch back to ServicePulse and click "Retry Message".

**VO:** "Fix the condition — failure rate back to zero. Click retry."

**[Editor note]** Highlight the "Retry" button just before the click.

### Beat 10 — success (~10s)

**Do:** Let ServicePulse update (message leaves the failed list). Switch to Rider console — show successful handler log line.

**VO:** "Message replays. Handler succeeds."

### Beat 11 — closing beat (~6s)

**Do:** Wide shot of ServicePulse dashboard.

**VO:** "No message is ever lost. The platform holds them until you decide what to do."

---

## Scene 6 — Observability (4:30–5:15)

**Starting state:** Everything still running from scene 5.

### Beat 1 — generate clean trace (~5s)

**Do:** Dashboard → "Place order".

**VO:** "One more order — clean trace, no retries."

### Beat 2 — open Jaeger (~3s)

**Do:** Switch to Jaeger tab. Service dropdown set to `Acme.Sales`.

**VO:** "OpenTelemetry is wired in. Here's Jaeger."

### Beat 3 — open most recent trace (~5s)

**Do:** Click the most recent trace entry. No VO.

### Beat 4 — walk the spans (~12s)

**Do:** Expand the trace. Show spans for Sales receive PlaceOrder → publish OrderPlaced → Billing receive OrderPlaced → Billing handle.

**VO:** "Command arrives at Sales. Sales publishes the event. Subscriber receives it, handler runs. One trace across publisher and subscriber. Nothing I wrote."

**[Editor note]** Highlight the span chain across the publish/receive boundary.

### Beat 5 — switch to ServicePulse monitoring (~3s)

**Do:** Switch to ServicePulse → Monitoring tab.

**VO:** "And ServicePulse monitoring."

### Beat 6 — metrics tour (~12s)

**Do:** Show the live chart with throughput, retry rate, processing time per endpoint. Scroll/hover to Billing to show the retry spike from scene 5.

**VO:** "Throughput per endpoint, per message type. Retry rate — the spike from the failures earlier is still there. Processing time, percentiles."

**[Editor note]** Circle the retry spike with a "from scene 5" label.

### Beat 7 — transition beat (~5s)

**Do:** Wide shot of monitoring dashboard.

**VO:** "All from those four lines I typed earlier. No tracing code, no metrics code, no custom instrumentation."

---

## Scene 7 — Outro (5:15–6:00) — added in post

**No recording needed.** See `slides.md` for slide content.

**VO recorded separately:**

> "There's more in NServiceBus. A bridge connects IBM MQ to RabbitMQ, Azure Service Bus, SQS, or Kafka. Sagas give you long-running stateful workflows. The outbox pattern gives you exactly-once-effective processing with your database. And message mutators let you integrate legacy formats — EBCDIC, fixed-length records, whatever's on the wire.
>
> Full docs at docs.particular.net. Thanks for watching."

---

## Editor handoff checklist

After recording, send the editor:

1. All raw recordings (screen + VO).
2. `presenter-script.md` (this file) — for VO-to-visual alignment.
3. `slides.md` — to generate slides for scenes 1, 2, 7.
4. List of callouts flagged as `[Editor note]` above.
5. Lower-third text — one per scene (suggested):
   - Scene 3: "Building a subscriber"
   - Scene 4: "Real MQ queues. Real MQ topics."
   - Scene 5: "Errors you can inspect and replay"
   - Scene 6: "Traces and metrics — free"
````

- [ ] **Step 2: Verify the file**

Run: `test -f docs/video/presenter-script.md && grep -c "^## Scene" docs/video/presenter-script.md`
Expected: file exists, contains exactly 7 `## Scene` headings.

- [ ] **Step 3: Read it out loud from top to bottom**

Mental check: does the VO flow naturally when spoken? Any tongue-twisters, over-long sentences, or scene transitions that feel abrupt? Fix inline if so. Target: a presenter should be able to read this without stumbling.

- [ ] **Step 4: Commit**

```bash
git add docs/video/presenter-script.md
git commit -m "📝 Add presenter script for IBM MQ announcement video"
```

---

## Task 5: Write `slides.md`

**Files:**
- Create: `docs/video/slides.md`

**Purpose:** Exact copy for scenes 1, 2, 7. Handed to whoever designs the actual slide deck (Keynote / PowerPoint / web). Includes VO reference for timing but the primary use is slide text.

- [ ] **Step 1: Write the file**

````markdown
# Slide Copy — IBM MQ Transport Announcement Video

Three slides total. All added in post-production; no live recording of slides.

Branding note: follow existing Particular.net / NServiceBus visual identity. Include IBM MQ branding (IBM MQ logo, standard IBM blue accent) in a lockup with the NServiceBus logo on the title and end cards.

---

## Slide 1 — Title card (0:00–0:15)

**Layout:** Centered.

**Primary text (largest):**

> NServiceBus IBM MQ Transport

**Subtitle (secondary, below the title):**

> Generally available

**Footer (small, bottom):** Particular Software logo, NServiceBus wordmark.

**VO reference (timing):**

> "The NServiceBus IBM MQ transport is generally available."

---

## Slide 2 — Agenda (0:15–0:30)

**Layout:** Four-bullet list, left-aligned, numbered.

**Header:**

> In the next five minutes

**Bullets:**

1. Build a subscriber from scratch
2. See queues and topic subscriptions appear in IBM MQ
3. Handle and replay a failure
4. Trace it end-to-end

**VO reference:**

> "In the next five minutes: build a subscriber from scratch, see how it shows up in IBM MQ, handle a failure, and trace it end-to-end."

---

## Slide 7 — Outro / more features + docs (5:15–6:00)

**Layout:** Header at top. Four feature bullets middle. URLs footer.

**Header:**

> There's more in NServiceBus.

**Feature bullets (fade in one at a time as VO mentions each):**

- **Bridge** — Connect IBM MQ to RabbitMQ, Azure Service Bus, SQS, or Kafka
- **Sagas** — Long-running stateful workflows with persisted state
- **Outbox** — Exactly-once-effective processing with your database
- **Message mutators** — Plug in legacy formats (EBCDIC, fixed-length records)

**URLs (bottom, visible for the last ~8 seconds):**

- `docs.particular.net`
- `nuget.org/packages/NServiceBus.Transport.IBMMQ`

**End card (final 3 seconds):** NServiceBus + IBM MQ logo lockup, single URL — `docs.particular.net`.

**VO reference:**

> "There's more in NServiceBus. A bridge connects IBM MQ to RabbitMQ, Azure Service Bus, SQS, or Kafka. Sagas give you long-running stateful workflows. The outbox pattern gives you exactly-once-effective processing with your database. And message mutators let you integrate legacy formats — EBCDIC, fixed-length records, whatever's on the wire.
>
> Full docs at docs.particular.net. Thanks for watching."

---

## Lower-thirds (used during scenes 3–6)

These are not slides but on-screen text overlays dropped in during editing. Keep style consistent with the slide deck.

| Scene | Lower-third text | Appears during |
|-------|------------------|----------------|
| 3 | Building a subscriber | First ~5s of scene |
| 4 | Real MQ queues. Real MQ topics. | First ~5s of scene |
| 5 | Errors you can inspect and replay | First ~5s of scene |
| 6 | Traces and metrics — free | First ~5s of scene |
````

- [ ] **Step 2: Verify the file**

Run: `test -f docs/video/slides.md && grep -c "^## Slide" docs/video/slides.md`
Expected: file exists, contains exactly 3 `## Slide` headings (scenes 1, 2, 7).

- [ ] **Step 3: Commit**

```bash
git add docs/video/slides.md
git commit -m "📝 Add slide copy for IBM MQ announcement video"
```

---

## Task 6: Write `demo-setup-and-reset.md`

**Files:**
- Create: `docs/video/demo-setup-and-reset.md`

**Purpose:** Commands and procedures to (a) bring the demo stack into a clean "ready to record" state, (b) strip `Acme.Billing` down to scene 3's starting state, (c) reset state between takes so failed messages / retries / traces from previous attempts don't pollute the recording.

- [ ] **Step 1: Write the file**

````markdown
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
````

- [ ] **Step 2: Verify the file**

Run: `test -f docs/video/demo-setup-and-reset.md && grep -c "^## Procedure" docs/video/demo-setup-and-reset.md`
Expected: file exists, contains exactly 3 `## Procedure` sections.

- [ ] **Step 3: Dry-run procedures 1 and 3 against the actual stack**

Run procedure 1 steps 1–4 against a fresh `just down && just up` cycle. Verify the smoke test passes. Then run procedure 3 steps 2–7 against a test failure run to confirm the MQSC commands work and ServicePulse clears as described. Fix any command-syntax errors inline.

- [ ] **Step 4: Commit**

```bash
git add docs/video/demo-setup-and-reset.md
git commit -m "📝 Add demo setup and reset procedures for video recording"
```

---

## Task 7: Self-check against the design spec

**Files:**
- Modify: any artifacts that turn out to be missing spec coverage.

**Purpose:** Compare the produced artifacts against the design spec line by line; fix gaps.

- [ ] **Step 1: Open both documents side by side**

```bash
$EDITOR docs/superpowers/specs/2026-04-24-ibmmq-video-design.md docs/video/presenter-script.md
```

- [ ] **Step 2: For each scene in the spec, verify the presenter script covers every beat**

Walk through scenes 1–7 in the spec. For each beat, confirm the corresponding beat in the presenter script matches on VO, on-screen action, and post-processing callouts.

If any beat is missing or diverges, fix inline in `docs/video/presenter-script.md`.

- [ ] **Step 3: Verify slide copy matches spec**

Check `docs/video/slides.md` against the spec's scenes 1, 2, 7 — headers, bullets, URLs, VO reference all present.

- [ ] **Step 4: Verify reference source matches the "final state" described in the spec**

Check `docs/video/reference/Program.cs` against spec scene 3 beat 3 (transport config) and scene 5 beat 2 (platform wiring). Check `reference/OrderPlacedHandler.cs` against scene 3 beat 5.

- [ ] **Step 5: Commit fixes (if any)**

```bash
git add docs/video/
git commit -m "📝 Align video artifacts with design spec"
```

If no fixes are needed, skip this step.

---

## Done

After Task 7, all artifacts are ready. The presenter can proceed with recording (following `demo-setup-and-reset.md` → `presenter-script.md`) and hand off to the editor (with `slides.md` + recordings).
