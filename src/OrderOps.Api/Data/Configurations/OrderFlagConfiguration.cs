using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Data.Configurations;

public sealed class OrderFlagConfiguration : IEntityTypeConfiguration<OrderFlag>
{
    public void Configure(EntityTypeBuilder<OrderFlag> b)
    {
        b.ToTable("order_flags");
        b.HasKey(f => f.OrderId);
        b.Property(f => f.OrderId).HasColumnName("order_id").HasColumnType("varchar(16)");
        b.Property(f => f.FlaggedAt).HasColumnName("flagged_at").HasColumnType("timestamptz").IsRequired();
        b.Property(f => f.SourceJobId).HasColumnName("source_job_id").HasColumnType("varchar(32)");
        b.Property(f => f.Reason).HasColumnName("reason").HasColumnType("text");
    }
}
