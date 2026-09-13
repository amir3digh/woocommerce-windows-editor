using Microsoft.EntityFrameworkCore;

namespace WooCommerceProductManager.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(product => product.LocalId);
            entity.Property(product => product.LocalId).ValueGeneratedOnAdd();
            entity.HasIndex(product => product.WooCommerceId).IsUnique();
            entity.Property(product => product.Name).IsRequired();
            entity.Property(product => product.Sku).HasMaxLength(256);
            entity.Property(product => product.RegularPrice).HasMaxLength(64);
            entity.Property(product => product.SalePrice).HasMaxLength(64);
            entity.Property(product => product.Price).HasMaxLength(64);
            entity.Property(product => product.StockStatus).HasMaxLength(64);
            entity.Property(product => product.DateModified).HasMaxLength(64);
            entity.Property(product => product.Permalink).HasMaxLength(2048);
            entity.Property(product => product.IsDirty).HasDefaultValue(false);
        });
    }
}
