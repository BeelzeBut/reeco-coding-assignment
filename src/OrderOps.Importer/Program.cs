using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NpgsqlTypes;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
var connStr = builder.Configuration.GetConnectionString("Postgres")
              ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

var dataDir = ResolveDataDir();
var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.sql");

Console.WriteLine($"[importer] data dir : {dataDir}");
Console.WriteLine($"[importer] schema   : {schemaPath}");

var sw = Stopwatch.StartNew();

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine($"[importer] connected to {conn.Host}:{conn.Port}/{conn.Database}");

await ApplySchemaAsync(conn, schemaPath);
Console.WriteLine("[importer] schema applied");

var categories = ReadCsv<CategoryRow>(Path.Combine(dataDir, "categories.csv"));
var suppliers  = ReadCsv<SupplierRow>(Path.Combine(dataDir, "suppliers.csv"));
var products   = ReadCsv<ProductRow>(Path.Combine(dataDir, "products.csv"));
var orders     = ReadCsv<OrderRow>(Path.Combine(dataDir, "orders.csv"));
Console.WriteLine($"[importer] parsed   : {categories.Count} cat, {suppliers.Count} sup, {products.Count} prod, {orders.Count} ord");

// Reject orphan product → category references at parse time. The seed has 81 products
// pointing at cat_200, which doesn't exist in categories.csv. NULL-coerce those references
// so the FK stays enforced for genuine bugs and the DB never holds dangling pointers.
var categoryIds = categories.Select(c => c.Id).ToHashSet();
var orphanedProductCats = 0;
foreach (var p in products)
{
    if (!string.IsNullOrEmpty(p.CategoryId) && !categoryIds.Contains(p.CategoryId))
    {
        p.CategoryId = null;
        orphanedProductCats++;
    }
}
if (orphanedProductCats > 0)
    Console.WriteLine($"[importer] fixup   : NULL-coerced {orphanedProductCats} product category_id orphan(s)");

await using (var tx = await conn.BeginTransactionAsync())
{
    await ExecAsync(conn, tx, "SET CONSTRAINTS ALL DEFERRED");
    await CopyCategoriesAsync(conn, categories);
    await CopySuppliersAsync(conn, suppliers);
    await CopyProductsAsync(conn, products);
    await CopyOrdersAsync(conn, orders);
    await tx.CommitAsync();
}
Console.WriteLine("[importer] COPY complete; FK constraints validated on commit");

var ok = true;
ok &= await VerifyCountAsync(conn, "categories", categories.Count);
ok &= await VerifyCountAsync(conn, "suppliers",  suppliers.Count);
ok &= await VerifyCountAsync(conn, "products",   products.Count);
ok &= await VerifyCountAsync(conn, "orders",     orders.Count);

sw.Stop();
if (!ok)
{
    Console.Error.WriteLine($"[importer] FAILED — count mismatch (elapsed {sw.Elapsed.TotalSeconds:F2}s)");
    return 1;
}
Console.WriteLine($"[importer] OK ({sw.Elapsed.TotalSeconds:F2}s)");
return 0;

static string ResolveDataDir()
{
    var dir = Directory.GetCurrentDirectory();
    while (!string.IsNullOrEmpty(dir))
    {
        var candidate = Path.Combine(dir, "data");
        if (File.Exists(Path.Combine(candidate, "orders.csv")))
            return candidate;
        dir = Path.GetDirectoryName(dir);
    }
    throw new DirectoryNotFoundException(
        "Could not locate /data by walking up from " + Directory.GetCurrentDirectory());
}

static async Task ApplySchemaAsync(NpgsqlConnection conn, string path)
{
    var sql = await File.ReadAllTextAsync(path);
    await using var cmd = new NpgsqlCommand(sql, conn);
    await cmd.ExecuteNonQueryAsync();
}

static async Task ExecAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql)
{
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    await cmd.ExecuteNonQueryAsync();
}

static List<T> ReadCsv<T>(string path)
{
    using var reader = new StreamReader(path);
    var cfg = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
    using var csv = new CsvReader(reader, cfg);
    return csv.GetRecords<T>().ToList();
}

static string? Nz(string? s) => string.IsNullOrEmpty(s) ? null : s;

static async Task CopyCategoriesAsync(NpgsqlConnection conn, List<CategoryRow> rows)
{
    await using var w = await conn.BeginBinaryImportAsync(
        "COPY categories (id, name, parent_id) FROM STDIN (FORMAT BINARY)");
    foreach (var r in rows)
    {
        await w.StartRowAsync();
        await w.WriteAsync(r.Id, NpgsqlDbType.Varchar);
        await w.WriteAsync(r.Name, NpgsqlDbType.Text);
        var p = Nz(r.ParentId);
        if (p is null) await w.WriteNullAsync();
        else           await w.WriteAsync(p, NpgsqlDbType.Varchar);
    }
    await w.CompleteAsync();
}

static async Task CopySuppliersAsync(NpgsqlConnection conn, List<SupplierRow> rows)
{
    await using var w = await conn.BeginBinaryImportAsync(
        "COPY suppliers (id, name, email, rating, country, active, created_at) FROM STDIN (FORMAT BINARY)");
    foreach (var r in rows)
    {
        await w.StartRowAsync();
        await w.WriteAsync(r.Id, NpgsqlDbType.Varchar);
        await w.WriteAsync(r.Name, NpgsqlDbType.Text);

        var email = Nz(r.Email);
        if (email is null) await w.WriteNullAsync(); else await w.WriteAsync(email, NpgsqlDbType.Text);

        if (r.Rating is null) await w.WriteNullAsync(); else await w.WriteAsync(r.Rating.Value, NpgsqlDbType.Numeric);

        var country = Nz(r.Country);
        if (country is null) await w.WriteNullAsync(); else await w.WriteAsync(country, NpgsqlDbType.Varchar);

        await w.WriteAsync(r.Active, NpgsqlDbType.Boolean);
        await w.WriteAsync(r.CreatedAt, NpgsqlDbType.TimestampTz);
    }
    await w.CompleteAsync();
}

static async Task CopyProductsAsync(NpgsqlConnection conn, List<ProductRow> rows)
{
    await using var w = await conn.BeginBinaryImportAsync(
        "COPY products (id, name, category_id, sku, price) FROM STDIN (FORMAT BINARY)");
    foreach (var r in rows)
    {
        await w.StartRowAsync();
        await w.WriteAsync(r.Id, NpgsqlDbType.Varchar);
        await w.WriteAsync(r.Name, NpgsqlDbType.Text);

        var cat = Nz(r.CategoryId);
        if (cat is null) await w.WriteNullAsync(); else await w.WriteAsync(cat, NpgsqlDbType.Varchar);

        var sku = Nz(r.Sku);
        if (sku is null) await w.WriteNullAsync(); else await w.WriteAsync(sku, NpgsqlDbType.Text);

        await w.WriteAsync(r.Price, NpgsqlDbType.Numeric);
    }
    await w.CompleteAsync();
}

static async Task CopyOrdersAsync(NpgsqlConnection conn, List<OrderRow> rows)
{
    await using var w = await conn.BeginBinaryImportAsync(
        "COPY orders (id, supplier_id, product_id, quantity, unit_price, total_price, status, priority, created_at, updated_at, warehouse, notes, version) FROM STDIN (FORMAT BINARY)");
    foreach (var r in rows)
    {
        await w.StartRowAsync();
        await w.WriteAsync(r.Id,         NpgsqlDbType.Varchar);
        await w.WriteAsync(r.SupplierId, NpgsqlDbType.Varchar);
        await w.WriteAsync(r.ProductId,  NpgsqlDbType.Varchar);
        await w.WriteAsync(r.Quantity,   NpgsqlDbType.Integer);
        await w.WriteAsync(r.UnitPrice,  NpgsqlDbType.Numeric);
        await w.WriteAsync(r.TotalPrice, NpgsqlDbType.Numeric);
        await w.WriteAsync(r.Status,     NpgsqlDbType.Varchar);
        await w.WriteAsync(r.Priority,   NpgsqlDbType.Varchar);
        await w.WriteAsync(r.CreatedAt,  NpgsqlDbType.TimestampTz);
        await w.WriteAsync(r.UpdatedAt,  NpgsqlDbType.TimestampTz);

        var wh = Nz(r.Warehouse);
        if (wh is null) await w.WriteNullAsync(); else await w.WriteAsync(wh, NpgsqlDbType.Varchar);

        var notes = Nz(r.Notes);
        if (notes is null) await w.WriteNullAsync(); else await w.WriteAsync(notes, NpgsqlDbType.Text);

        await w.WriteAsync(1, NpgsqlDbType.Integer);
    }
    await w.CompleteAsync();
}

static async Task<bool> VerifyCountAsync(NpgsqlConnection conn, string table, int expected)
{
    await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", conn);
    var actual = (long)(await cmd.ExecuteScalarAsync())!;
    if (actual != expected)
    {
        Console.Error.WriteLine($"[importer] {table}: expected {expected}, got {actual}");
        return false;
    }
    Console.WriteLine($"[importer] {table,-11}: {actual} rows");
    return true;
}

public sealed class CategoryRow
{
    [Name("id")]        public string Id { get; set; } = "";
    [Name("name")]      public string Name { get; set; } = "";
    [Name("parent_id")] public string? ParentId { get; set; }
}

public sealed class SupplierRow
{
    [Name("id")]         public string Id { get; set; } = "";
    [Name("name")]       public string Name { get; set; } = "";
    [Name("email")]      public string? Email { get; set; }
    [Name("rating")]     public decimal? Rating { get; set; }
    [Name("country")]    public string? Country { get; set; }
    [Name("active")]     public bool Active { get; set; }
    [Name("created_at")] public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ProductRow
{
    [Name("id")]          public string Id { get; set; } = "";
    [Name("name")]        public string Name { get; set; } = "";
    [Name("category_id")] public string? CategoryId { get; set; }
    [Name("sku")]         public string? Sku { get; set; }
    [Name("price")]       public decimal Price { get; set; }
}

public sealed class OrderRow
{
    [Name("id")]          public string Id { get; set; } = "";
    [Name("supplier_id")] public string SupplierId { get; set; } = "";
    [Name("product_id")]  public string ProductId { get; set; } = "";
    [Name("quantity")]    public int Quantity { get; set; }
    [Name("unit_price")]  public decimal UnitPrice { get; set; }
    [Name("total_price")] public decimal TotalPrice { get; set; }
    [Name("status")]      public string Status { get; set; } = "";
    [Name("priority")]    public string Priority { get; set; } = "";
    [Name("created_at")]  public DateTimeOffset CreatedAt { get; set; }
    [Name("updated_at")]  public DateTimeOffset UpdatedAt { get; set; }
    [Name("warehouse")]   public string? Warehouse { get; set; }
    [Name("notes")]       public string? Notes { get; set; }
}
