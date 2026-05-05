using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).HasColumnName("id").HasColumnType("varchar(16)");
        b.Property(o => o.SupplierId).HasColumnName("supplier_id").HasColumnType("varchar(16)").IsRequired();
        b.Property(o => o.ProductId).HasColumnName("product_id").HasColumnType("varchar(16)").IsRequired();
        b.Property(o => o.Quantity).HasColumnName("quantity").IsRequired();
        b.Property(o => o.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(12,2)").IsRequired();
        b.Property(o => o.TotalPrice).HasColumnName("total_price").HasColumnType("numeric(14,2)").IsRequired();
        b.Property(o => o.Status).HasColumnName("status").HasColumnType("varchar(16)").IsRequired();
        b.Property(o => o.Priority).HasColumnName("priority").HasColumnType("varchar(16)").IsRequired();
        b.Property(o => o.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        b.Property(o => o.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
        b.Property(o => o.Warehouse).HasColumnName("warehouse").HasColumnType("varchar(32)");
        b.Property(o => o.Notes).HasColumnName("notes").HasColumnType("text");
        b.Property(o => o.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        b.HasOne(o => o.Supplier).WithMany(s => s.Orders).HasForeignKey(o => o.SupplierId);
        b.HasOne(o => o.Product).WithMany(p => p.Orders).HasForeignKey(o => o.ProductId);
        b.HasOne(o => o.Flag).WithOne(f => f.Order).HasForeignKey<OrderFlag>(f => f.OrderId);
    }
}
