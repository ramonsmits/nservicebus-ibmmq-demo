namespace Acme;

public record OrderPlaced(Guid OrderId, string Product, int Quantity) : IEvent;
public record OrderShipped(Guid OrderId, string TrackingNumber) : IEvent;

public record ExpressOrderPlaced(Guid OrderId, string Product, int Quantity) : OrderPlaced(OrderId, Product, Quantity);
