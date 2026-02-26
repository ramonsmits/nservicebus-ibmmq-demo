using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Transport;

namespace Acme;

public sealed class ControlQueueFeature : Feature
{
    public ControlQueueFeature()
    {
#pragma warning disable CS0618
        EnableByDefault();
#pragma warning restore CS0618
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var endpointName = context.Settings.EndpointName();

        context.AddSatelliteReceiver(
            name: "Control",
            transportAddress: new QueueAddress(endpointName + ".control"),
            runtimeSettings: new PushRuntimeSettings(maxConcurrency: 1),
            recoverabilityPolicy: (config, errorContext) =>
                RecoverabilityAction.MoveToError(config.Failed.ErrorQueue),
            onMessage: (sp, mc, ct) => OnMessage(endpointName, sp, mc, ct));
    }

    static async Task OnMessage(string endpointName, IServiceProvider serviceProvider, MessageContext messageContext, CancellationToken cancellationToken)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<ControlQueueFeature>();
        var enclosedTypes = messageContext.Headers.GetValueOrDefault("NServiceBus.EnclosedMessageTypes", "");

        if (enclosedTypes.Contains(nameof(GetSimulationSettings)))
        {
            await SendSettingsResponse(endpointName, serviceProvider, messageContext, logger);
            return;
        }

        var settings = JsonSerializer.Deserialize<UpdateSimulationSettings>(messageContext.Body.Span);
        if (settings != null)
        {
            var concurrencyChanged = settings.Concurrency > 0 && settings.Concurrency != SimulationSettings.Concurrency;
            var transactionModeChanged = !string.IsNullOrEmpty(settings.TransactionMode) && settings.TransactionMode != SimulationSettings.TransactionMode;

            SimulationSettings.Apply(settings);
            SimulationSettings.Save(settings);

            logger.LogInformation("Control: updated simulation settings {{ FailurePercentage = {FailurePercentage}, ProcessingDelayMs = {ProcessingDelayMs} }}",
                settings.FailurePercentage, settings.ProcessingDelayMs);

            await SendSettingsResponse(endpointName, serviceProvider, messageContext, logger);

            if (concurrencyChanged || transactionModeChanged)
            {
                var reason = concurrencyChanged ? $"Concurrency changed to {settings.Concurrency}" : $"TransactionMode changed to {settings.TransactionMode}";
                logger.LogInformation("{Reason}, stopping endpoint for restart", reason);
                // Defer shutdown so this handler returns first, allowing the endpoint to drain
                // in-flight messages before the DI container is disposed
                _ = Task.Run(() => serviceProvider.GetService<IHostApplicationLifetime>()?.StopApplication());
            }
        }
    }

    static async Task SendSettingsResponse(string endpointName, IServiceProvider serviceProvider, MessageContext messageContext, ILogger logger)
    {
        if (!messageContext.Headers.TryGetValue("NServiceBus.ReplyToAddress", out var replyTo))
        {
            return;
        }

        var session = serviceProvider.GetRequiredService<IMessageSession>();
        var response = new SimulationSettingsResponse(
            endpointName,
            SimulationSettings.FailurePercentage,
            SimulationSettings.ProcessingDelayMs,
            SimulationSettings.Concurrency,
            SimulationSettings.TransactionMode);

        var options = new SendOptions();
        options.SetDestination(replyTo);
        await session.Send(response, options);

        logger.LogInformation("Control: sent settings response to {ReplyTo}", replyTo);
    }
}
