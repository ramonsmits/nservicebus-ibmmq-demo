using System.Threading.Channels;

namespace Acme;

public class EventStreamService
{
    readonly object _lock = new();
    readonly List<Channel<string>> _clients = [];

    public ChannelReader<string> Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        lock (_lock)
        {
            _clients.Add(channel);
        }

        return channel.Reader;
    }

    public void Unsubscribe(ChannelReader<string> reader)
    {
        lock (_lock)
        {
            _clients.RemoveAll(c => c.Reader == reader);
        }
    }

    public void Broadcast(string message)
    {
        lock (_lock)
        {
            foreach (var client in _clients)
            {
                client.Writer.TryWrite(message);
            }
        }
    }
}
