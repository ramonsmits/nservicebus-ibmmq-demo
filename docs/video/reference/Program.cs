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
