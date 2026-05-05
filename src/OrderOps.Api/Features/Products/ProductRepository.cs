using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderOps.Api.Data;
using OrderOps.Api.Infrastructure;

namespace OrderOps.Api.Features.Products;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public async Task<PagedResult<ProductListItem>> ListAsync(int limit, int offset, CancellationToken ct)
    {
        var total = await _db.Products.CountAsync(ct);

        var rows = await _db.Products.AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip(offset).Take(limit)
            .Select(p => new ProductListItem(p.Id, p.Name, p.CategoryId, p.Sku, p.Price))
            .ToListAsync(ct);

        return new PagedResult<ProductListItem>(rows, total, limit, offset);
    }

    // Cycle guard via visited-array path: c.id = ANY(d.path) marks revisit; outer
    // WHERE NOT d.cycle blocks descent. Required because seed has cat_150↔151↔152.
    public async Task<PagedResult<ProductListItem>> ListByCategoryDescendantsAsync(
        string rootCategoryId, int limit, int offset, CancellationToken ct)
    {
        const string descendantsCte = """
            WITH RECURSIVE descendants AS (
                SELECT id, parent_id, ARRAY[id::text] AS path, false AS cycle
                FROM   categories
                WHERE  id = {0}
                UNION ALL
                SELECT c.id, c.parent_id, d.path || c.id::text, c.id::text = ANY(d.path)
                FROM   categories c
                JOIN   descendants d ON c.parent_id = d.id
                WHERE  NOT d.cycle
            )
            """;

        var rows = await _db.Database
            .SqlQueryRaw<ProductListItem>(
                descendantsCte + """
                SELECT id          AS "Id",
                       name        AS "Name",
                       category_id AS "CategoryId",
                       sku         AS "Sku",
                       price       AS "Price"
                FROM   products
                WHERE  category_id IN (SELECT id FROM descendants)
                ORDER  BY id
                LIMIT  {1} OFFSET {2}
                """,
                rootCategoryId, limit, offset)
            .ToListAsync(ct);

        var total = await _db.Database
            .SqlQueryRaw<long>(
                descendantsCte + """
                SELECT count(*)::bigint AS "Value"
                FROM   products
                WHERE  category_id IN (SELECT id FROM descendants)
                """,
                rootCategoryId)
            .FirstAsync(ct);

        return new PagedResult<ProductListItem>(rows, total, limit, offset);
    }
}
