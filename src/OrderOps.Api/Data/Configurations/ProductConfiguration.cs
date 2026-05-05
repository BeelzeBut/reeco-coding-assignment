using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").HasColumnType("varchar(16)");
        b.Property(p => p.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        b.Property(p => p.CategoryId).HasColumnName("category_id").HasColumnType("varchar(16)");
        b.Property(p => p.Sku).HasColumnName("sku").HasColumnType("text");
        b.Property(p => p.Price).HasColumnName("price").HasColumnType("numeric(12,2)").IsRequired();

        b.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId);
    }
}
