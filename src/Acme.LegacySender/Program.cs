using System.Buffers.Binary;
using System.Collections;
using System.Text;
using IBM.WMQ;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var host = Environment.GetEnvironmentVariable("IBMMQ_HOST") ?? "localhost";
var port = int.Parse(Environment.GetEnvironmentVariable("IBMMQ_PORT") ?? "1414");
var channel = Environment.GetEnvironmentVariable("IBMMQ_CHANNEL") ?? "APP.SVRCONN";
var user = Environment.GetEnvironmentVariable("IBMMQ_USER") ?? "fmainframe";

var ebcdic = Encoding.GetEncoding("IBM500");

var props = new Hashtable
{
    { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED },
    { MQC.HOST_NAME_PROPERTY, host },
    { MQC.PORT_PROPERTY, port },
    { MQC.CHANNEL_PROPERTY, channel },
    { MQC.USE_MQCSP_AUTHENTICATION_PROPERTY, false },
    { MQC.USER_ID_PROPERTY, user },
};
try
{
    using var qm = new MQQueueManager("QM1", props);
    using var queue = qm.AccessQueue("Acme.Sales", MQC.MQOO_OUTPUT);

    Console.WriteLine("Acme.LegacySender (EBCDIC) started.");

    // --send N: non-interactive batch mode
    var sendCount = args.Length >= 2 && args[0] == "--send" && int.TryParse(args[1], out var n) ? n : 0;

    if (sendCount > 0)
    {
        for (var i = 0; i < sendCount; i++)
            SendOrder(queue, ebcdic);
        return;
    }

    Console.WriteLine("Press [P] to place an order, [Q] to quit.");

    while (true)
    {
        var key = Console.ReadKey(true);

        if (key.Key is ConsoleKey.Q)
            break;

        if (key.Key is ConsoleKey.P)
            SendOrder(queue, ebcdic);
    }
}
catch (Exception e)
{
    Console.WriteLine(e);
    throw;
}

static void SendOrder(MQQueue queue, Encoding ebcdic)
{
    var orderId = Guid.NewGuid();
    var product = "Widget";
    var quantity = Random.Shared.Next(1, 10);

    var body = new byte[70];

    // OrderId: 36 bytes EBCDIC
    ebcdic.GetBytes(orderId.ToString(), body.AsSpan(0, 36));

    // Product: 30 bytes EBCDIC, space-padded
    var productSpan = body.AsSpan(36, 30);
    productSpan.Fill(ebcdic.GetBytes(" ")[0]); // EBCDIC space = 0x40
    ebcdic.GetBytes(product, productSpan);

    // Quantity: 4 bytes big-endian int32
    BinaryPrimitives.WriteInt32BigEndian(body.AsSpan(66, 4), quantity);

    var msg = new MQMessage
    {
        CharacterSet = 500, // EBCDIC IBM500 International
        Format = MQC.MQFMT_NONE
    };
    msg.Write(body);

    queue.Put(msg);

    Console.WriteLine($"Sent EBCDIC PlaceOrder {{ OrderId = {orderId}, Product = {product}, Quantity = {quantity} }}");
}
