using Microsoft.Extensions.Logging;

namespace Acme;

sealed class OrderShippedHandler(ILogger<OrderShippedHandler> logger) : IHandleMessages<OrderShipped>
{
    public Task Handle(OrderShipped message, IMessageHandlerContext context)
    {
        logger.LogInformation("Received OrderShipped {{ OrderId = {OrderId}, TrackingNumber = {TrackingNumber} }}", message.OrderId, message.TrackingNumber);
        return Task.CompletedTask;
    }
}
