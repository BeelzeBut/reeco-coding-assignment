using Dapper;
using Npgsql;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Products;

public sealed class ProductRepository : IProductRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ProductRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<PagedResult<ProductListItem>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        const string sql = """
            SELECT id, name, category_id, sku, price
            FROM   products
            ORDER  BY id
            LIMIT  @limit OFFSET @offset;

            SELECT count(*) FROM products;
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { limit, offset }, cancellationToken: ct));

        var rows = (await grid.ReadAsync<ProductListItem>()).AsList();
        var total = await grid.ReadSingleAsync<long>();
        return new PagedResult<ProductListItem>(rows, total, limit, offset);
    }

    // Cycle guard via visited-array path: c.id = ANY(d.path) marks revisit; outer
    // WHERE NOT d.cycle blocks descent. Required because seed has cat_150↔151↔152.
    public async Task<PagedResult<ProductListItem>> ListByCategoryDescendantsAsync(
        string rootCategoryId, int limit, int offset, CancellationToken ct)
    {
        const string sql = """
            WITH RECURSIVE descendants AS (
                SELECT id, parent_id, ARRAY[id::text] AS path, false AS cycle
                FROM   categories
                WHERE  id = @rootId
                UNION ALL
                SELECT c.id, c.parent_id, d.path || c.id::text, c.id::text = ANY(d.path)
                FROM   categories c
                JOIN   descendants d ON c.parent_id = d.id
                WHERE  NOT d.cycle
            )
            SELECT id, name, category_id, sku, price
            FROM   products
            WHERE  category_id IN (SELECT id FROM descendants)
            ORDER  BY id
            LIMIT  @limit OFFSET @offset;

            WITH RECURSIVE descendants AS (
                SELECT id, parent_id, ARRAY[id::text] AS path, false AS cycle
                FROM   categories
                WHERE  id = @rootId
                UNION ALL
                SELECT c.id, c.parent_id, d.path || c.id::text, c.id::text = ANY(d.path)
                FROM   categories c
                JOIN   descendants d ON c.parent_id = d.id
                WHERE  NOT d.cycle
            )
            SELECT count(*)
            FROM   products
            WHERE  category_id IN (SELECT id FROM descendants);
            """;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(
            sql, new { rootId = rootCategoryId, limit, offset }, cancellationToken: ct));

        var rows = (await grid.ReadAsync<ProductListItem>()).AsList();
        var total = await grid.ReadSingleAsync<long>();
        return new PagedResult<ProductListItem>(rows, total, limit, offset);
    }
}
