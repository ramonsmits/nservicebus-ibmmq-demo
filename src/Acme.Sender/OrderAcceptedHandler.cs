namespace Acme;

sealed class OrderAcceptedHandler : IHandleMessages<OrderAccepted>
{
    public Task Handle(OrderAccepted message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Received OrderAccepted {{ OrderId = {message.OrderId} }}");
        return Task.CompletedTask;
    }
}
