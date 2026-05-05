using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Data.Configurations;

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.ToTable("suppliers");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasColumnName("id").HasColumnType("varchar(16)");
        b.Property(s => s.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        b.Property(s => s.Email).HasColumnName("email").HasColumnType("text");
        b.Property(s => s.Rating).HasColumnName("rating").HasColumnType("numeric(3,2)");
        b.Property(s => s.Country).HasColumnName("country").HasColumnType("varchar(8)");
        b.Property(s => s.Active).HasColumnName("active").IsRequired();
        b.Property(s => s.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
    }
}
