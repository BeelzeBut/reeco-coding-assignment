using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderOps.Api.Data.Entities;

namespace OrderOps.Api.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).HasColumnName("id").HasColumnType("varchar(16)");
        b.Property(c => c.Name).HasColumnName("name").HasColumnType("text").IsRequired();
        b.Property(c => c.ParentId).HasColumnName("parent_id").HasColumnType("varchar(16)");

        b.HasOne(c => c.Parent)
            .WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId);
    }
}
