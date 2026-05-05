using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace OrderOps.Api.Features.Events;

[ApiController]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly byte[] HeartbeatFrame = Encoding.UTF8.GetBytes(": ping\n\n");
    private static readonly byte[] RetryHintFrame = Encoding.UTF8.GetBytes("retry: 5000\n\n");

    private readonly IEventHub _hub;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<EventsController> _log;

    public EventsController(
        IEventHub hub,
        IHostApplicationLifetime lifetime,
        IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> mvcJson,
        ILogger<EventsController> log)
    {
        _hub = hub;
        _lifetime = lifetime;
        _jsonOptions = mvcJson.Value.JsonSerializerOptions;
        _log = log;
    }

    [HttpGet]
    public async Task Stream([FromQuery(Name = "supplier_id")] string? supplierId, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Connection  = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.StartAsync(ct);
        await Response.Body.WriteAsync(RetryHintFrame, ct);
        await Response.Body.FlushAsync(ct);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.ApplicationStopping);
        var token = linked.Token;

        var subscriberId = _hub.Subscribe(supplierId, out var reader);

        try
        {
            using var heartbeat = new PeriodicTimer(HeartbeatInterval);
            var heartbeatTask = heartbeat.WaitForNextTickAsync(token).AsTask();
            var readTask = reader.WaitToReadAsync(token).AsTask();

            while (!token.IsCancellationRequested)
            {
                var winner = await Task.WhenAny(heartbeatTask, readTask);

                if (winner == heartbeatTask)
                {
                    var ticked = await heartbeatTask;
                    if (!ticked) break;
                    await Response.Body.WriteAsync(HeartbeatFrame, token);
                    await Response.Body.FlushAsync(token);
                    heartbeatTask = heartbeat.WaitForNextTickAsync(token).AsTask();
                    continue;
                }

                var hasMore = await readTask;
                if (!hasMore) break;

                while (reader.TryRead(out var envelope))
                {
                    var json = JsonSerializer.Serialize(envelope, _jsonOptions);
                    var frame = Encoding.UTF8.GetBytes("data: " + json + "\n\n");
                    await Response.Body.WriteAsync(frame, token);
                    await Response.Body.FlushAsync(token);
                }

                readTask = reader.WaitToReadAsync(token).AsTask();
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or host shutdown — normal exit.
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SSE stream error for subscriber {Id}", subscriberId);
        }
        finally
        {
            _hub.Unsubscribe(subscriberId);
        }
    }
}
