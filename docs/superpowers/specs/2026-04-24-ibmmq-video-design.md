# NServiceBus IBM MQ Transport — Announcement Video Design

**Date:** 2026-04-24
**Purpose:** YouTube video to embed in the blog post at `ibm.test.particular.net/blog/nservicebus-transport-ibmmq`
**Target length:** ~6 minutes
**Audience:** IBM MQ practitioners unfamiliar with NServiceBus and the Particular Platform

## Goals

1. Announce the NServiceBus IBM MQ transport (generally available).
2. Convince IBM MQ viewers that NServiceBus offers:
   - **Consistency guarantees** out of the box (transactional receive + send, no custom transaction code).
   - **Multi-year stability** — 15+ years of production middleware.
   - A first-class .NET developer experience on top of their existing IBM MQ infrastructure.
3. Demonstrate three capabilities viewers can verify with their own eyes:
   - Creating an endpoint — ~15 lines of config + one handler — with automatic queue and topic-subscription provisioning in IBM MQ.
   - Error handling via ServicePulse: failed messages land in the error queue, are inspectable, and can be replayed with one click.
   - Observability: distributed traces in Jaeger and live metrics in ServicePulse, both for free from a handful of configuration lines.
4. Close with a short feature teaser (bridge to other transports, sagas, outbox, message mutators) and a link to `docs.particular.net`.

## Non-goals

- Not a tutorial on how to install IBM MQ.
- Not a tutorial on the Particular Platform stack (ServiceControl, ServicePulse, Jaeger setup).
- Not a deep dive on the bridge — it is mentioned as a feature in the outro only.
- Not demoing sagas, outbox, or EBCDIC legacy integration on camera.
- No live-webinar cadence — the video is edited, so recording format (one take, many takes, VO post-sync) is not constrained by this design.

## Style

- **Demo-forward.** Slides appear only at the start (title + agenda) and the end (outro). Both are added in post-production.
- **Snappy pacing.** 2026 YouTube style — tight cuts, no filler.
- **Tone:** matter-of-fact, confident, zero hype. Credibility comes from showing working systems in the viewer's own tooling (IBM MQ Web Console, Jaeger).

## Structure overview

| # | Scene | Time window | Content type |
|---|-------|-------------|--------------|
| 1 | Title + announcement | 0:00–0:15 | Slide (post) |
| 2 | Agenda | 0:15–0:30 | Slide (post) |
| 3 | Build a subscriber live in Rider | 0:30–2:00 | Screen recording |
| 4 | Queues + topic subscription appear in IBM MQ | 2:00–2:45 | Screen recording |
| 5 | Failure → error queue → ServicePulse replay | 2:45–4:30 | Screen recording |
| 6 | Observability (Jaeger trace + ServicePulse monitoring) | 4:30–5:15 | Screen recording |
| 7 | Outro — more features + docs | 5:15–6:00 | Slide (post) |

**Total: ~6:00.**

## Demo setup assumptions

- The demo stack in this repo (`podman compose up`) is running: IBM MQ, RabbitMQ, RavenDB, Jaeger, the existing `Acme.Sales`, `Acme.Shipping`, `Acme.Dashboard`, `Acme.Bridge`, and both ServiceControl stacks.
- `Acme.Billing` is the endpoint rebuilt on camera. Its `Program.cs` is reduced to a blank stub and its `OrderPlacedHandler.cs` is removed *before recording*. The `.csproj` keeps package references to `NServiceBus`, `NServiceBus.Transport.IBMMQ`, hosting packages, and a project reference to `Acme.Shared`.
- `OpenTelemetry` configuration is **pre-baked** in `Program.cs` (a small `services.AddOpenTelemetry()…AddOtlpExporter()` block). It is not discussed on camera because it is generic .NET telemetry wiring.
- `Recoverability()` configuration is **pre-baked** with demo-friendly values (`Immediate(0)`, `Delayed(0)`, `OnConsecutiveFailures(3, RateLimitSettings(5s))`) so failures surface quickly. The pre-baked block is glanced at during scene 5 but not typed.
- Platform-integration lines (`AuditProcessedMessagesTo`, `SendFailedMessagesTo`, `SendHeartbeatTo`, `EnableMetrics().SendMetricDataToServiceControl`) are **typed live** at the start of scene 5.
- Browser tabs are open in advance:
  - IBM MQ Web Console — `https://localhost:9443`
  - `Acme.Dashboard` — `http://localhost:6001`
  - ServicePulse (IBM MQ instance) — `http://localhost:9090`
  - Jaeger — `http://localhost:16686`
- Reference source on a second monitor or laptop for the presenter.

## Scene-by-scene design

### Scene 1 — Title + announcement (0:00–0:15)

**Medium:** Slide, added in post.

**Content:** Title card — "NServiceBus IBM MQ Transport" with subtitle "Generally available." Simple background, Particular + IBM MQ branding as appropriate.

**Voiceover (1 sentence):** "The NServiceBus IBM MQ transport is generally available."

### Scene 2 — Agenda (0:15–0:30)

**Medium:** Slide, added in post.

**Content — 4 bullets:**

1. Build a subscriber from scratch
2. See queues and topic subscriptions appear in IBM MQ
3. Handle and replay a failure
4. Trace it end-to-end

**Voiceover:** "In the next five minutes: build a subscriber from scratch, see how it shows up in IBM MQ, handle a failure, and trace it end-to-end."

### Scene 3 — Build a subscriber live (0:30–2:00)

**Medium:** Rider screen recording.

**Starting state:**

- `Acme.Billing.csproj` exists with package references ready.
- `Program.cs` contains a pre-baked `services.AddOpenTelemetry()…AddOtlpExporter()` block at the top and nothing else below it. The presenter types the `Host.CreateDefaultBuilder…UseNServiceBus(…)` block *underneath* the existing OTel block.
- No `OrderPlacedHandler.cs`.
- `Recoverability()` config is added off-camera between scenes 4 and 5. At scene 3's end, the only NServiceBus code in `Program.cs` is transport configuration — no recoverability, no audit/error queues, no heartbeats. That keeps the "fifteen lines, one handler" pitch accurate.
- OTel is acknowledged with one VO line at scene 3 beat 1 ("standard OTel wiring is already here"), then the presenter scrolls down and works below it. The "fifteen lines" count refers strictly to the NServiceBus block typed on camera.

**Beats:**

| # | Sec | What's on screen | What's said |
|---|-----|------------------|-------------|
| 1 | 5 | Rider solution view, Billing project selected; `Program.cs` showing only the pre-baked OTel block | "Empty console app. Message contracts already referenced. Standard OTel wiring is already here — not the point today." |
| 2 | 5 | `.csproj` with `NServiceBus` + `NServiceBus.Transport.IBMMQ` visible | "Transport ships on public NuGet." |
| 3 | 25 | Live-type `Host.CreateDefaultBuilder(...).UseNServiceBus(context => { ... })` with `new EndpointConfiguration("Acme.Billing")`, `new IBMMQTransport { Host, Port=1414, QueueManagerName="QM1", Channel="APP.SVRCONN", User="billing" }`, `endpointConfiguration.UseTransport(transport)`, `endpointConfiguration.EnableInstallers()` | "Name the endpoint. Wire up the transport — the same connection values any MQ client uses. `EnableInstallers` auto-provisions queues and topic subscriptions in dev." |
| 4 | 10 | Cursor on transport config | "Transport is transactional by default. Message receive and outgoing sends commit atomically. Handler throws, message rolls back to the queue. No custom transaction code." |
| 5 | 20 | Create `OrderPlacedHandler.cs`, implement `IHandleMessages<OrderPlaced>`, log the order | "One handler class. Strongly-typed contract. Log it." |
| 6 | 10 | Whole `Program.cs` visible on one screen | "That's the endpoint. Fifteen lines. This programming model has been in production for more than fifteen years." |
| 7 | 5 | Finger on the run button | "Let's run it." |

**Post-processing callouts:**

- Arrow/zoom on `UseTransport(transport)`.
- Tooltip on `EnableInstallers()` — "auto-creates queues + topic subscriptions in dev".
- Highlight on `IHandleMessages<OrderPlaced>`.
- Optional transient text overlay during beat 4: "Transactional receive + send" / "At-least-once, idempotent-safe".

### Scene 4 — Queues + topics in IBM MQ (2:00–2:45)

**Medium:** Mix of IBM MQ Web Console and Rider console, shown in browser and IDE.

**Beats:**

| # | Sec | What's on screen | What's said |
|---|-----|------------------|-------------|
| 1 | 5 | Rider console — endpoint startup banner | "Endpoint starts up, opens a channel to QM1." |
| 2 | 10 | MQ Web Console → Queues panel, `Acme.Billing` highlighted | "There's my endpoint queue — NServiceBus created it on startup because I enabled installers." |
| 3 | 15 | MQ Web Console → Subscriptions, new sub on `DEV.ACME.ORDERPLACED` pointing to `Acme.Billing` | "Under subscriptions — a subscription on `DEV.ACME.ORDERPLACED`, routing to my queue. Native IBM MQ pub/sub. No proxy, no shadow scheme — NServiceBus wires up real MQ primitives." |
| 4 | 10 | Dashboard → "Place order" button clicked | "Acme.Sales is the publisher — already running. Place an order from the dashboard." |
| 5 | 5 | Rider console shows `Received OrderPlaced { ... }` | "Handler fires. End-to-end pub/sub over IBM MQ." |

**Rhetorical move:** Showing the actual topic subscription in IBM MQ's own administration UI kills the assumption that NServiceBus layers a shadow pub/sub scheme on top of MQ.

**Post-processing callouts:**

- Red outline on the `Acme.Billing` queue row.
- Red outline on the `DEV.ACME.ORDERPLACED` subscription row.
- Arrow from the "Place order" button to the Rider console log entry.

### Scene 5 — Failure, error queue, replay (2:45–4:30)

**Medium:** Rider → Dashboard → IBM MQ Web Console → ServicePulse.

**Pre-recording state:** `Recoverability()` block already in `Program.cs` (pre-baked).

**Beats:**

| # | Sec | What's on screen | What's said |
|---|-----|------------------|-------------|
| 1 | 8 | Cursor in `Program.cs` after the transport + handler config | "So far the endpoint runs, but the platform doesn't know about it. Let me wire it in." |
| 2 | 20 | Live-type `AuditProcessedMessagesTo("audit")`, `SendFailedMessagesTo("error")`, `SendHeartbeatTo("Particular.ServiceControl")`, `.EnableMetrics().SendMetricDataToServiceControl("Particular.Monitoring", TimeSpan.FromSeconds(10))` | "Audit — successful messages copied to an audit queue. Error — failures go here. Heartbeat — so the platform knows I'm alive. Metrics — throughput, retry rates, processing time. Four lines, opt-in." |
| 3 | 8 | Scroll briefly to the pre-baked `Recoverability()` block, highlight | "Retries are configured here — three attempts, then the message is considered failed." |
| 4 | 5 | Stop + run | "Restart." |
| 5 | 8 | Dashboard → simulation panel → Billing failure rate set to 100% | "On the dashboard, set this endpoint's failure rate to a hundred percent." |
| 6 | 12 | Dashboard places order → Rider console shows retry log lines → final exception | "Place an order. Retries happen. All three fail." |
| 7 | 8 | MQ Web Console → `error` queue → depth 1 → open message, show headers | "Message on the `error` queue. All NServiceBus headers preserved, full stack trace." |
| 8 | 15 | ServicePulse → Failed Messages → group for `OrderPlacedHandler` → click in → exception, headers, body | "In ServicePulse, failures group by root cause. Click in — full exception, headers, body, everything." |
| 9 | 15 | Dashboard → failure rate back to 0% → ServicePulse → click "Retry Message" | "Fix the condition — failure rate back to zero. Click retry." |
| 10 | 10 | ServicePulse updates → Rider console shows successful handler log | "Message replays. Handler succeeds." |
| 11 | 6 | Wide shot of ServicePulse dashboard | "No message is ever lost. The platform holds them until you decide what to do." |

**Rhetorical move:** IBM MQ has its own `SYSTEM.DEAD.LETTER.QUEUE` but admins usually handle those with MQSC scripts or manual tooling. A web UI with per-failure grouping, exception inspection, and one-click replay is the differentiator.

**Post-processing callouts:**

- Highlight each typed line as it appears.
- Underline the retry log lines in the Rider console.
- Zoom into the MQ message headers panel.
- Highlight the "Retry" button before it's clicked.

### Scene 6 — Observability (4:30–5:15)

**Medium:** Dashboard → Jaeger → ServicePulse.

**Beats:**

| # | Sec | What's on screen | What's said |
|---|-----|------------------|-------------|
| 1 | 5 | Dashboard → "Place order" | "One more order — clean trace, no retries." |
| 2 | 3 | Jaeger UI, service = `Acme.Sales` | "OpenTelemetry is wired in. Here's Jaeger." |
| 3 | 5 | Click most recent trace | — |
| 4 | 12 | Expanded trace: Sales receive PlaceOrder → publish OrderPlaced → Billing receive → Billing handle | "Command arrives at Sales. Sales publishes the event. Subscriber receives it, handler runs. One trace across publisher and subscriber. Nothing I wrote." |
| 5 | 3 | ServicePulse → Monitoring tab | "And ServicePulse monitoring." |
| 6 | 12 | Live charts — throughput, retry rate, processing time. Scroll to Billing with earlier retry spike visible. | "Throughput per endpoint, per message type. Retry rate — the spike from the failures earlier is still there. Processing time, percentiles." |
| 7 | 5 | Wide shot | "All from those four lines I typed earlier. No tracing code, no metrics code, no custom instrumentation." |

**Trace scope:** Only pure IBM MQ traces (Dashboard → Sales → Billing). Cross-transport traces via the bridge are avoided because `NServiceBus.MessagingBridge` 5.x does not emit OpenTelemetry spans (documented in `CHALLENGES.md` §6).

**Post-processing callouts:**

- Highlight the span chain across the publish/receive boundary.
- Circle the retry spike on the ServicePulse monitoring chart with a "from scene 5" label.

### Scene 7 — Outro (5:15–6:00)

**Medium:** Slide, added in post.

**Header:** "There's more in NServiceBus."

**Feature bullets (4):**

- **Bridge** — connect IBM MQ to RabbitMQ, Azure Service Bus, SQS, or Kafka.
- **Sagas** — long-running stateful workflows with persisted state.
- **Outbox** — exactly-once-effective processing with your database.
- **Message mutators** — plug in legacy formats (EBCDIC, fixed-length records).

**Footer — URLs:**

- `docs.particular.net` (primary)
- `nuget.org/packages/NServiceBus.Transport.IBMMQ`
- Link to the announcement blog post

**Voiceover:**

> There's more in NServiceBus. A bridge connects IBM MQ to RabbitMQ, Azure Service Bus, SQS, or Kafka. Sagas give you long-running stateful workflows. The outbox pattern gives you exactly-once-effective processing with your database. And message mutators let you integrate legacy formats — EBCDIC, fixed-length records, whatever's on the wire.
>
> Full docs at docs.particular.net. Thanks for watching.

**Post-processing:**

- Fade-in per feature as the VO mentions it.
- URLs visible for the last ~8 seconds.
- End card (final 3 seconds): NServiceBus + IBM MQ logos, single URL.

## Narrative spine (one-sentence per scene)

- **Scene 3:** "Look how thin this is — transport and handler, that's it."
- **Scene 4:** "Queues and topic subscriptions are real IBM MQ objects, not a shadow layer."
- **Scene 5:** "Failures never get lost — they land in the error queue and you replay them from a UI."
- **Scene 6:** "Tracing and metrics come for free from four config lines."
- **Scene 7:** "And here's everything else you can do."

## Open questions / presenter decisions

These are not blocking the plan — they are presenter choices to confirm before shooting:

1. Whether to keep the Sender CLI visible at all or hide it entirely. Current design: hidden; Dashboard triggers all orders.
2. Exact text for on-screen lower-thirds / chapter markers — decided in the production plan.
3. Whether to keep the Dashboard's simulation panel visible during scenes 4 and 6 (adds clutter) or collapse it between scenes. Current design: collapsed when not in use.

## Follow-ups after this design

- **Production plan** (next step): exact `Program.cs` snippets and `OrderPlacedHandler` code for scene 3, verbatim slide copy for scenes 1/2/7, a presenter checklist (reset demo state between takes, pre-populated orders for monitoring chart realism), and recording checklist.
- **Blog post integration:** embed the final video above the fold in the announcement post; the first ~15 seconds (scene 1 + scene 2) should stand alone as a preview thumbnail summary.
