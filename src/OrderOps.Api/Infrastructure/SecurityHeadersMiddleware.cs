namespace OrderOps.Api.Infrastructure;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(static state =>
        {
            var headers = ((HttpContext)state).Response.Headers;
            headers["X-Content-Type-Options"]      = "nosniff";
            headers["X-Frame-Options"]             = "DENY";
            headers["Referrer-Policy"]             = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            return Task.CompletedTask;
        }, ctx);

        return _next(ctx);
    }
}
