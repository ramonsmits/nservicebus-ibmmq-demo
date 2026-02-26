using Microsoft.Extensions.Logging;

namespace Acme;

sealed class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger) : IHandleMessages<OrderPlaced>
{
    public Task Handle(OrderPlaced message, IMessageHandlerContext context)
    {
        logger.LogInformation("Received OrderPlaced {{ OrderId = {OrderId}, Product = {Product}, Quantity = {Quantity} }}", message.OrderId, message.Product, message.Quantity);
        return Task.CompletedTask;
    }
}
