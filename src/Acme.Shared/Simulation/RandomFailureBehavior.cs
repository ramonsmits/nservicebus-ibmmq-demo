using NServiceBus.Logging;
using NServiceBus.Pipeline;

namespace Acme;

public sealed class RandomFailureBehavior : Behavior<IIncomingLogicalMessageContext>
{
    static readonly ILog log = LogManager.GetLogger<RandomFailureBehavior>();

    public override async Task Invoke(IIncomingLogicalMessageContext context, Func<Task> next)
    {
        if (context.Message.MessageType == typeof(UpdateSimulationSettings) ||
            context.Message.MessageType == typeof(SimulationSettingsResponse))
        {
            await next();
            return;
        }

        var messageType = context.Message.MessageType.Name;

        log.Info($"Processing {messageType}");

        var delayMs = SimulationSettings.ProcessingDelayMs;
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, context.CancellationToken);
        }

        if (SimulationSettings.ShouldFail())
        {
            throw new InvalidOperationException(
                $"Simulated failure processing {messageType} (every {SimulationSettings.FailureInterval} messages)");
        }

        await next();

        log.Info($"Processed {messageType}");
    }
}
