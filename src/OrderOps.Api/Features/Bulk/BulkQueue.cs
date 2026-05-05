using System.Threading.Channels;

namespace OrderOps.Api.Features.Bulk;

public sealed class BulkQueue
{
    private readonly Channel<BulkJob> _channel = Channel.CreateUnbounded<BulkJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ChannelWriter<BulkJob> Writer => _channel.Writer;
    public ChannelReader<BulkJob> Reader => _channel.Reader;
}
