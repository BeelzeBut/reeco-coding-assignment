using OrderOps.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:3000");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1 * 1024 * 1024);

builder.Services
    .AddOrderOpsPostgres(builder.Configuration)
    .AddOrderOpsRedis(builder.Configuration)
    .AddOrderOpsCors(builder.Configuration)
    .AddOrderOpsFeatures();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => JsonOptionsConfig.Apply(o.JsonSerializerOptions));

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseRouting();
app.UseCors(ServiceCollectionExtensions.FrontendCorsPolicy);
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
