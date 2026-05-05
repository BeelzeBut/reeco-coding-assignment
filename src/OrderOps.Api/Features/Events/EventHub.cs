using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OrderOps.Api.Features.Events;

public sealed class EventHub : IEventHub, IAsyncDisposable
{
    private const int SubscriberChannelCapacity = 256;
    private const string BulkCompletedType = "bulk_completed";

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private readonly ILogger<EventHub> _log;

    public EventHub(ILogger<EventHub> log) => _log = log;

    public Guid Subscribe(string? supplierFilter, out ChannelReader<EventEnvelope> reader)
    {
        var channel = Channel.CreateBounded<EventEnvelope>(new BoundedChannelOptions(SubscriberChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var id = Guid.NewGuid();
        var subscriber = new Subscriber(channel, supplierFilter);
        _subscribers[id] = subscriber;
        reader = channel.Reader;

        _log.LogDebug("EventHub subscribe id={Id} filter={Filter} total={Total}",
            id, supplierFilter ?? "<none>", _subscribers.Count);

        return id;
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var subscriber))
        {
            subscriber.Channel.Writer.TryComplete();
            _log.LogDebug("EventHub unsubscribe id={Id} total={Total}", id, _subscribers.Count);
        }
    }

    public ValueTask PublishAsync(EventEnvelope envelope)
    {
        var isBulk = string.Equals(envelope.Type, BulkCompletedType, StringComparison.Ordinal);

        foreach (var (id, subscriber) in _subscribers)
        {
            if (!isBulk && subscriber.SupplierFilter is not null
                && !string.Equals(subscriber.SupplierFilter, envelope.SupplierId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!subscriber.Channel.Writer.TryWrite(envelope))
            {
                _log.LogWarning("EventHub dropped event for subscriber {Id} (channel full or completed)", id);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var (_, subscriber) in _subscribers)
        {
            subscriber.Channel.Writer.TryComplete();
        }
        _subscribers.Clear();
        return ValueTask.CompletedTask;
    }

    private sealed record Subscriber(Channel<EventEnvelope> Channel, string? SupplierFilter);
}
