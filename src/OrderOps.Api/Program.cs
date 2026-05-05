using OrderOps.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:3000");

builder.Services
    .AddOrderOpsPostgres(builder.Configuration)
    .AddOrderOpsRedis(builder.Configuration)
    .AddOrderOpsFeatures();

builder.Services
    .AddControllers()
    .AddJsonOptions(o => JsonOptionsConfig.Apply(o.JsonSerializerOptions));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlerMiddleware>();
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
