using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderOps.Api.Data;
using OrderOps.Api.Features.Bulk;
using OrderOps.Api.Features.Orders;
using OrderOps.Api.Features.Products;
using OrderOps.Api.Features.Suppliers;
using StackExchange.Redis;

namespace OrderOps.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderOpsPostgres(this IServiceCollection services, IConfiguration cfg)
    {
        var connStr = cfg.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        var dataSource = new NpgsqlDataSourceBuilder(connStr).Build();
        services.AddSingleton(dataSource);
        services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(dataSource));
        return services;
    }

    public static IServiceCollection AddOrderOpsRedis(this IServiceCollection services, IConfiguration cfg)
    {
        var connStr = cfg.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        var opts = ConfigurationOptions.Parse(connStr);
        opts.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(opts));
        return services;
    }

    public static IServiceCollection AddOrderOpsFeatures(this IServiceCollection services)
    {
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBulkRepository, BulkRepository>();
        services.AddScoped<OrdersService>();
        services.AddScoped<SuppliersService>();
        services.AddScoped<ProductsService>();
        services.AddScoped<BulkService>();

        services.AddSingleton<BulkQueue>();
        services.AddSingleton<IJobStateStore, RedisJobStateStore>();
        services.AddHostedService<BulkWorker>();

        return services;
    }
}
