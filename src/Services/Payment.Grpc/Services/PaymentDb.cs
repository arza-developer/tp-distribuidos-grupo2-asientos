using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

public class PaymentDbContext : DbContext
{
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(t => t.TransactionId)
            .IsUnique();
    }
}