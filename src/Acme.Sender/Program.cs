using Acme;
using NServiceBus.Transport.IBMMQ;

var endpointConfiguration = new EndpointConfiguration("Acme.Sender");

endpointConfiguration.UseSerialization<SystemJsonSerializer>();

var transport = new IBMMQTransport(o =>
{
    o.Host = Environment.GetEnvironmentVariable("IBMMQ_HOST") ?? "localhost";
    o.Port = 1414;
    o.QueueManagerName = "QM1";
    o.Channel = Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ?? "APP.SVRCONN";
    o.User = Environment.GetEnvironmentVariable("IBMMQ_USER") ?? "sender";
});

var routing = endpointConfiguration.UseTransport(transport);
endpointConfiguration.EnableInstallers();

routing.RouteToEndpoint(typeof(PlaceOrder), "Acme.Sales");

endpointConfiguration.Recoverability()
    .Immediate(i => i.NumberOfRetries(0))
    .Delayed(d => d.NumberOfRetries(0))
    .OnConsecutiveFailures(3, new RateLimitSettings(TimeSpan.FromSeconds(5)));

endpointConfiguration.LimitMessageProcessingConcurrencyTo(SimulationSettings.Concurrency);
endpointConfiguration.Pipeline.Register(new RandomFailureBehavior(), "Randomly fails messages for demo purposes");

endpointConfiguration.AuditProcessedMessagesTo("audit");
endpointConfiguration.SendFailedMessagesTo("error");

endpointConfiguration.SendHeartbeatTo("Particular.ServiceControl");
// Demo only: 0.5s interval for near-real-time dashboard updates. Use 10s+ in production.
endpointConfiguration.EnableMetrics().SendMetricDataToServiceControl("Particular.Monitoring", TimeSpan.FromMilliseconds(500));

var endpointInstance = await Endpoint.Start(endpointConfiguration);

Console.WriteLine("Acme.Sender started.");
Console.WriteLine("Press [P] to place an order, [Q] to quit.");

while (true)
{
    var key = Console.ReadKey(true);

    if (key.Key == ConsoleKey.Q)
    {
        break;
    }

    if (key.Key == ConsoleKey.P)
    {
        var orderId = Guid.NewGuid();
        var quantity = Random.Shared.Next(1, 10);
        await endpointInstance.Send(new PlaceOrder(orderId, "Widget", quantity));
        Console.WriteLine($"Sent PlaceOrder {{ OrderId = {orderId}, Product = Widget, Quantity = {quantity} }}");
    }
}

await endpointInstance.Stop();
