using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

public class LogiTrackContext : DbContext
{
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=logitrack.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // One Order has many InventoryItems; each InventoryItem belongs to at most one Order
        modelBuilder.Entity<Order>()
            .HasMany(o => o.OrderList)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}