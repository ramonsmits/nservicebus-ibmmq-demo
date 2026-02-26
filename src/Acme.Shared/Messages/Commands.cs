namespace Acme;

public record PlaceOrder(Guid OrderId, string Product, int Quantity) : ICommand;
public record ShipOrder(Guid OrderId, string Address) : ICommand;
public record UpdateSimulationSettings(int FailurePercentage, int ProcessingDelayMs, int Concurrency, string TransactionMode = "SendsAtomicWithReceive") : ICommand;
public record GetSimulationSettings : ICommand;
public record SimulationSettingsResponse(string Endpoint, int FailurePercentage, int ProcessingDelayMs, int Concurrency, string TransactionMode = "SendsAtomicWithReceive") : IMessage;
