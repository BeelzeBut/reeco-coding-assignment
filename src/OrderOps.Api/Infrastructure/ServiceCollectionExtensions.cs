using Dapper;
using Npgsql;
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

        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var dataSource = new NpgsqlDataSourceBuilder(connStr).Build();
        services.AddSingleton(dataSource);
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
        services.AddScoped<OrdersService>();
        services.AddScoped<SuppliersService>();
        services.AddScoped<ProductsService>();
        return services;
    }
}
