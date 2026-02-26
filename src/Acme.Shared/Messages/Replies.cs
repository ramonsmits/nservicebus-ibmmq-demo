using NServiceBus;

namespace Acme;

public record OrderAccepted(Guid OrderId) : IMessage;
public record ShipmentConfirmed(Guid OrderId, string TrackingNumber) : IMessage;
