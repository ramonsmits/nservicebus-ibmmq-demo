using Acme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

Acme.StartupBanner.Log();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("Acme.Shipping"))
            .WithTracing(t => t.SetSampler(new AlwaysOnSampler()).AddSource("NServiceBus.*").AddOtlpExporter())
            .WithMetrics(m => m.AddMeter("NServiceBus.*").AddOtlpExporter());
    })
    .UseNServiceBus(context =>
    {
        var endpointConfiguration = new EndpointConfiguration("Acme.Shipping");

        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
        endpointConfiguration.CustomDiagnosticsWriter((d, _) =>
        {
            NServiceBus.Logging.LogManager.GetLogger("StartupDiagnostics").Debug(d);
            return Task.CompletedTask;
        });

        var connectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING")
            ?? "host=rabbitmq;username=guest;password=guest";

        var transport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            connectionString);

        var txMode = SimulationSettings.TransportTransactionMode;
        if (txMode != TransportTransactionMode.ReceiveOnly)
            txMode = TransportTransactionMode.ReceiveOnly;
        transport.TransportTransactionMode = txMode;
        endpointConfiguration.UseTransport(transport);
        endpointConfiguration.EnableInstallers();

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
    })
    .Build();

await host.RunAsync();
