using System.Threading.Channels;

namespace OrderOps.Api.Features.Events;

public interface IEventHub
{
    Guid Subscribe(string? supplierFilter, out ChannelReader<EventEnvelope> reader);
    void Unsubscribe(Guid id);
    ValueTask PublishAsync(EventEnvelope envelope);
}
