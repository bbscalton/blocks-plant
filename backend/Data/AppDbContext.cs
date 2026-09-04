using BlocksPlant.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BlocksPlant.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductionEntry> ProductionEntries => Set<ProductionEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<RecipeLine> RecipeLines => Set<RecipeLine>();
    public DbSet<MaterialTransaction> MaterialTransactions => Set<MaterialTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Sale>()
            .HasIndex(s => s.ClientId)
            .IsUnique();

        modelBuilder.Entity<ProductionEntry>()
            .HasIndex(p => p.ClientId)
            .IsUnique();

        modelBuilder.Entity<RecipeLine>()
            .HasIndex(r => new { r.ProductId, r.RawMaterialId })
            .IsUnique();

        modelBuilder.Entity<Product>()
            .Property(p => p.PricePerBlock)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Customer>()
            .Property(c => c.Balance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Sale>()
            .Property(s => s.Subtotal).HasPrecision(18, 2);
        modelBuilder.Entity<Sale>()
            .Property(s => s.AmountPaid).HasPrecision(18, 2);
        modelBuilder.Entity<Sale>()
            .Property(s => s.BalanceDue).HasPrecision(18, 2);

        modelBuilder.Entity<SaleLine>()
            .Property(l => l.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<SaleLine>()
            .Property(l => l.LineTotal).HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<RawMaterial>()
            .Property(m => m.QuantityOnHand).HasPrecision(18, 4);
        modelBuilder.Entity<RawMaterial>()
            .Property(m => m.MinStock).HasPrecision(18, 4);

        modelBuilder.Entity<RecipeLine>()
            .Property(r => r.QuantityPerBlock).HasPrecision(18, 6);

        modelBuilder.Entity<MaterialTransaction>()
            .Property(t => t.QuantityDelta).HasPrecision(18, 4);
        modelBuilder.Entity<MaterialTransaction>()
            .Property(t => t.QuantityAfter).HasPrecision(18, 4);
    }
}
