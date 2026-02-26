namespace Acme;

sealed class OrderShippedHandler(EventStreamService events) : IHandleMessages<OrderShipped>
{
    public Task Handle(OrderShipped message, IMessageHandlerContext context)
    {
        events.Broadcast($"OrderShipped {{ OrderId = {message.OrderId}, TrackingNumber = {message.TrackingNumber} }}");
        return Task.CompletedTask;
    }
}
