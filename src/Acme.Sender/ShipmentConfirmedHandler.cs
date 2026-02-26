namespace Acme;

sealed class ShipmentConfirmedHandler : IHandleMessages<ShipmentConfirmed>
{
    public Task Handle(ShipmentConfirmed message, IMessageHandlerContext context)
    {
        Console.WriteLine($"Received ShipmentConfirmed {{ OrderId = {message.OrderId}, TrackingNumber = {message.TrackingNumber} }}");
        return Task.CompletedTask;
    }
}
