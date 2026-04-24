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
