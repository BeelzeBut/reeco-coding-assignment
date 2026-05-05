using System.Text.Json;

namespace OrderOps.Api.Infrastructure;

public sealed class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _log;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DomainException ex)
        {
            await Write(ctx, ex.StatusCode, ex.Message, ex.Code);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await Write(ctx, 500, "Internal server error", "internal_error");
        }
    }

    private static Task Write(HttpContext ctx, int status, string error, string code)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error, code });
        return ctx.Response.WriteAsync(payload);
    }
}
