using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(
        DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");

            entity.HasKey(x => x.OrderId);

            entity.Property(x => x.OrderId)
                .HasColumnName("OrderId");

            entity.Property(x => x.UserId)
                .HasColumnName("UserId")
                .IsRequired();

            entity.Property(x => x.OrderStatus)
                .HasColumnName("OrderStatus")
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnName("CreatedAt")
                .IsRequired();

            entity.HasMany(x => x.OrderItems)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");

            entity.HasKey(x => x.OrderItemId);

            entity.Property(x => x.OrderItemId)
                .HasColumnName("OrderItemId");

            entity.Property(x => x.OrderId)
                .HasColumnName("OrderId")
                .IsRequired();

            entity.Property(x => x.ProductId)
                .HasColumnName("ProductId")
                .IsRequired();

            entity.Property(x => x.Quantity)
                .HasColumnName("Quantity")
                .IsRequired();
        });
    }
}