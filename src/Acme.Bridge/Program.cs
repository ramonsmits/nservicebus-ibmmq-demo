using Acme;
using Microsoft.Extensions.Hosting;
using NServiceBus.Transport.IBMMQ;

Acme.StartupBanner.Log();

var host = Host.CreateDefaultBuilder(args)
    .UseNServiceBusBridge(bridge =>
    {
        var ibmMqHost = Environment.GetEnvironmentVariable("IBMMQ_HOST") ?? "ibmmq";
        var rabbitMqConnectionString = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTIONSTRING")
            ?? "host=rabbitmq;username=guest;password=guest";

        // IBM MQ side
        var ibmMqTransport = new IBMMQTransport(o =>
        {
            o.Host = ibmMqHost;
            o.Port = 1414;
            o.QueueManagerName = "QM1";
            o.Channel = Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ?? "APP.SVRCONN";
            o.User = Environment.GetEnvironmentVariable("IBMMQ_USER") ?? "bridge";
        });

        var ibmMq = new BridgeTransport(ibmMqTransport) { AutoCreateQueues = true };
        ibmMq.HasEndpoint("Acme.Sender");
        ibmMq.HasEndpoint("Acme.Sales");

        var dashboardEndpoint = new BridgeEndpoint("Acme.Dashboard");
        dashboardEndpoint.RegisterPublisher<OrderShipped>("Acme.Shipping");
        ibmMq.HasEndpoint(dashboardEndpoint);

        var billingEndpoint = new BridgeEndpoint("Acme.Billing");
        billingEndpoint.RegisterPublisher<OrderShipped>("Acme.Shipping");
        ibmMq.HasEndpoint(billingEndpoint);


        // RabbitMQ side
        var rabbitMqTransport = new RabbitMQTransport(
            RoutingTopology.Conventional(QueueType.Quorum),
            rabbitMqConnectionString);

        var rabbitMq = new BridgeTransport(rabbitMqTransport) { AutoCreateQueues = true };

        var shippingEndpoint = new BridgeEndpoint("Acme.Shipping");
        shippingEndpoint.RegisterPublisher<OrderPlaced>("Acme.Sales");
        rabbitMq.HasEndpoint(shippingEndpoint);
        rabbitMq.HasEndpoint("Acme.Shipping.control");

        bridge.AddTransport(ibmMq);
        bridge.AddTransport(rabbitMq);
    })
    .Build();

await host.RunAsync();
