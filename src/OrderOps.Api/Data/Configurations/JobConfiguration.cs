using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Data.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("jobs");
        b.HasKey(j => j.Id);
        b.Property(j => j.Id).HasColumnName("id").HasColumnType("varchar(32)");
        b.Property(j => j.Status).HasColumnName("status").HasColumnType("varchar(16)").IsRequired();
        b.Property(j => j.Total).HasColumnName("total").IsRequired();
        b.Property(j => j.Completed).HasColumnName("completed").IsRequired();
        b.Property(j => j.Failed).HasColumnName("failed").IsRequired();
        b.Property(j => j.Action).HasColumnName("action").HasColumnType("varchar(16)").IsRequired();
        b.Property(j => j.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        b.Property(j => j.FinishedAt).HasColumnName("finished_at").HasColumnType("timestamptz");
    }
}
