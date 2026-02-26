using System.Buffers.Binary;
using System.Collections;
using System.Text;
using Acme;
using IBM.WMQ;
using NServiceBus.Transport.IBMMQ;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Acme.StartupBanner.Log();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EventStreamService>();
builder.Services.AddSingleton<SimulationSettingsCache>();
builder.Services.AddHostedService<SimulationSettingsStartup>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("Acme.Dashboard"))
    .WithTracing(t => t.SetSampler(new AlwaysOnSampler()).AddSource("NServiceBus.*").AddOtlpExporter())
    .WithMetrics(m => m.AddMeter("NServiceBus.*").AddOtlpExporter());

builder.Host.UseNServiceBus(context =>
{
    var endpointConfiguration = new EndpointConfiguration("Acme.Dashboard");

    endpointConfiguration.UseSerialization<SystemJsonSerializer>();
    endpointConfiguration.CustomDiagnosticsWriter((d, _) =>
    {
        NServiceBus.Logging.LogManager.GetLogger("StartupDiagnostics").Debug(d);
        return Task.CompletedTask;
    });

    var transport = new IBMMQTransport(o =>
    {
        o.Host = Environment.GetEnvironmentVariable("IBMMQ_HOST") ?? "localhost";
        o.Port = 1414;
        o.QueueManagerName = "QM1";
        o.Channel = Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ?? "APP.SVRCONN";
        o.User = Environment.GetEnvironmentVariable("IBMMQ_USER") ?? "dashboard";
    });

    transport.TransportTransactionMode = SimulationSettings.TransportTransactionMode;
    var routing = endpointConfiguration.UseTransport(transport);
    endpointConfiguration.EnableInstallers();

    routing.RouteToEndpoint(typeof(PlaceOrder), "Acme.Sales");

    endpointConfiguration.Recoverability()
        .Immediate(i => i.NumberOfRetries(0))
        .Delayed(d => d.NumberOfRetries(0))
        .OnConsecutiveFailures(3, new RateLimitSettings(TimeSpan.FromSeconds(5)));

    endpointConfiguration.LimitMessageProcessingConcurrencyTo(SimulationSettings.Concurrency);
    endpointConfiguration.Pipeline.Register(new RandomFailureBehavior(), "Randomly fails messages for demo purposes");

    endpointConfiguration.AuditProcessedMessagesTo("audit");
    endpointConfiguration.SendFailedMessagesTo("error");

    endpointConfiguration.SendHeartbeatTo("Particular.ServiceControl");
    // Demo only: 0.5s interval for near-real-time dashboard updates. Use 10s+ in production.
    endpointConfiguration.EnableMetrics().SendMetricDataToServiceControl("Particular.Monitoring", TimeSpan.FromMilliseconds(500));

    return endpointConfiguration;
});

var app = builder.Build();

app.UseStaticFiles();

app.MapPost("/api/place-order", async (IMessageSession session, EventStreamService events) =>
{
    var orderId = Guid.NewGuid();
    var quantity = Random.Shared.Next(1, 10);

    await session.Send(new PlaceOrder(orderId, "Widget", quantity));

    var message = $"Sent PlaceOrder {{ OrderId = {orderId}, Product = Widget, Quantity = {quantity} }}";
    events.Broadcast(message);

    return Results.Ok(new { orderId, message });
});

app.MapPost("/api/place-order-legacy", (EventStreamService events) =>
{
    var host = Environment.GetEnvironmentVariable("IBMMQ_HOST") ?? "localhost";
    var port = int.Parse(Environment.GetEnvironmentVariable("IBMMQ_PORT") ?? "1414");
    var channel = Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ?? "APP.SVRCONN";

    var ebcdic = Encoding.GetEncoding("IBM500");

    var props = new Hashtable
    {
        { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
        { MQC.HOST_NAME_PROPERTY, host },
        { MQC.PORT_PROPERTY, port },
        { MQC.CHANNEL_PROPERTY, channel },
        { MQC.USE_MQCSP_AUTHENTICATION_PROPERTY, false },
        { MQC.USER_ID_PROPERTY, "fmainframe" },
    };

    using var qm = new MQQueueManager("QM1", props);
    using var queue = qm.AccessQueue("Acme.Sales", MQC.MQOO_OUTPUT);

    var orderId = Guid.NewGuid();
    var product = "Widget";
    var quantity = Random.Shared.Next(1, 10);

    var body = new byte[70];
    ebcdic.GetBytes(orderId.ToString(), body.AsSpan(0, 36));
    var productSpan = body.AsSpan(36, 30);
    productSpan.Fill(ebcdic.GetBytes(" ")[0]);
    ebcdic.GetBytes(product, productSpan);
    BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(66, 4), quantity);

    var msg = new MQMessage
    {
        CharacterSet = 500,
        Format = MQC.MQFMT_NONE
    };
    msg.Write(body);
    queue.Put(msg);

    var message = $"Sent EBCDIC PlaceOrder {{ OrderId = {orderId}, Product = {product}, Quantity = {quantity} }}";
    events.Broadcast(message);

    return Results.Ok(new { orderId, message });
});

app.MapGet("/api/simulation/{endpoint}", (string endpoint, SimulationSettingsCache cache) =>
{
    var settings = cache.Get(endpoint);
    return settings is not null ? Results.Ok(settings) : Results.NoContent();
});

app.MapPost("/api/simulation/{endpoint}", async (string endpoint, UpdateSimulationSettings settings, IMessageSession session, EventStreamService events) =>
{
    var options = new SendOptions();
    options.SetDestination(endpoint + ".control");
    await session.Send(settings, options);

    events.Broadcast($"Sent UpdateSimulationSettings {{ FailurePercentage = {settings.FailurePercentage}, ProcessingDelayMs = {settings.ProcessingDelayMs}, Concurrency = {settings.Concurrency}, TransactionMode = {settings.TransactionMode} }} to {endpoint}");

    return Results.Ok();
});

app.MapGet("/api/events", async (EventStreamService events, HttpContext http, CancellationToken ct) =>
{
    http.Response.Headers.ContentType = "text/event-stream";
    http.Response.Headers.CacheControl = "no-cache";
    http.Response.Headers.Connection = "keep-alive";

    var reader = events.Subscribe();

    try
    {
        await foreach (var message in reader.ReadAllAsync(ct))
        {
            await http.Response.WriteAsync($"data: {message}\n\n", ct);
            await http.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected
    }
    finally
    {
        events.Unsubscribe(reader);
    }
});

app.MapFallback(async context =>
{
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "index.html"));
});

app.Run();
