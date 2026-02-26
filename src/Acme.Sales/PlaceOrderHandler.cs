using Microsoft.Extensions.Logging;

namespace Acme;

sealed class PlaceOrderHandler(ILogger<PlaceOrderHandler> logger) : IHandleMessages<PlaceOrder>
{
    public async Task Handle(PlaceOrder message, IMessageHandlerContext context)
    {
        await context.Reply(new OrderAccepted(message.OrderId));
        logger.LogInformation("Replied OrderAccepted {{ OrderId = {OrderId} }}", message.OrderId);

        await context.Publish(new ExpressOrderPlaced(message.OrderId, message.Product, message.Quantity));
        logger.LogInformation("Published OrderPlaced {{ OrderId = {OrderId} }}", message.OrderId);

        await context.Send(new ShipOrder(message.OrderId, "123 Main St"));
        logger.LogInformation("Sent ShipOrder {{ OrderId = {OrderId} }}", message.OrderId);
    }
}
