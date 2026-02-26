using Microsoft.Extensions.Logging;

namespace Acme;

sealed class ShipOrderHandler(ILogger<ShipOrderHandler> logger) : IHandleMessages<ShipOrder>
{
    public async Task Handle(ShipOrder message, IMessageHandlerContext context)
    {
        var trackingNumber = $"TRACK-{message.OrderId.ToString()[..8].ToUpperInvariant()}";

        await context.Reply(new ShipmentConfirmed(message.OrderId, trackingNumber));
        logger.LogInformation("Replied ShipmentConfirmed {{ OrderId = {OrderId}, TrackingNumber = {TrackingNumber} }}", message.OrderId, trackingNumber);

        await context.Publish(new OrderShipped(message.OrderId, trackingNumber));
        logger.LogInformation("Published OrderShipped {{ OrderId = {OrderId}, TrackingNumber = {TrackingNumber} }}", message.OrderId, trackingNumber);
    }
}
