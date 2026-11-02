using Microsoft.EntityFrameworkCore;

namespace Payment.Grpc.Services;

public class PaymentDbContext : DbContext
{
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<PaymentTransaction>()
            .HasIndex(t => t.CorrelationId)
            .IsUnique();
    }
}