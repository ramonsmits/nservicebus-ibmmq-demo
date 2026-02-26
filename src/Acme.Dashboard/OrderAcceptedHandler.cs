using NServiceBus;

namespace Acme;

public class OrderAcceptedHandler(EventStreamService events) : IHandleMessages<OrderAccepted>
{
    public Task Handle(OrderAccepted message, IMessageHandlerContext context)
    {
        events.Broadcast($"OrderAccepted {{ OrderId = {message.OrderId} }}");
        return Task.CompletedTask;
    }
}
