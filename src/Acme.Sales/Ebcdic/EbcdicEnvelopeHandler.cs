using NServiceBus.Logging;
using NServiceBus.MessageMutator;

namespace Acme;

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

sealed class EbcdicMessageMutator : IMutateIncomingTransportMessages
{
    static readonly ILog Log = LogManager.GetLogger<EbcdicMessageMutator>();
    static readonly Encoding Ebcdic = CodePagesEncodingProvider.Instance.GetEncoding("IBM500")!;
    const int RecordLength = 70;

    public Task MutateIncoming(MutateIncomingTransportMessageContext context)
    {
        var body = context.Body;

        Log.Info($"MutateIncoming: body={body.Length} headers={context.Headers.Count} keys=[{string.Join(",", context.Headers.Keys)}]");

        if (body.Length != RecordLength)
            return Task.CompletedTask;

        // Skip NServiceBus messages — they always have EnclosedMessageTypes
        if (context.Headers.ContainsKey(Headers.EnclosedMessageTypes))
            return Task.CompletedTask;

        Log.Info("Converting EBCDIC message");

        var span = body.Span;

        // Parse fixed-length EBCDIC record
        var orderId = Ebcdic.GetString(span[..36]);
        var product = Ebcdic.GetString(span[36..66]).TrimEnd();
        var quantity = BinaryPrimitives.ReadInt32BigEndian(span[66..70]);

        // Write JSON body
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("OrderId", orderId);
        writer.WriteString("Product", product);
        writer.WriteNumber("Quantity", quantity);
        writer.WriteEndObject();
        writer.Flush();

        context.Body = buffer.WrittenMemory;

        var messageType = typeof(PlaceOrder);
        var headers = context.Headers;

        if (!headers.ContainsKey(Headers.MessageId))
            headers[Headers.MessageId] = Guid.NewGuid().ToString();

        headers[Headers.ConversationId] = headers[Headers.MessageId];
        headers[Headers.EnclosedMessageTypes] = messageType.FullName + ", " + messageType.Assembly.GetName().Name;
        headers[Headers.ContentType] = "application/json";
        headers[Headers.MessageIntent] = nameof(MessageIntent.Send);
        headers[Headers.TimeSent] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:ffffff") + " Z";
        headers[Headers.ReplyToAddress] = "Acme.LegacySender";
        headers[Headers.OriginatingEndpoint] = "LegacyMainframe";
        headers[Headers.OriginatingMachine] = "MAINFRAME";

        return Task.CompletedTask;
    }
}
