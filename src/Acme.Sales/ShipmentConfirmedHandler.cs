using Microsoft.Extensions.Logging;
using NServiceBus;

namespace Acme;

sealed class ShipmentConfirmedHandler(ILogger<ShipmentConfirmedHandler> logger) : IHandleMessages<ShipmentConfirmed>
{
    public Task Handle(ShipmentConfirmed message, IMessageHandlerContext context)
    {
        logger.LogInformation("Received ShipmentConfirmed {{ OrderId = {OrderId}, TrackingNumber = {TrackingNumber} }}", message.OrderId, message.TrackingNumber);
        return Task.CompletedTask;
    }
}
