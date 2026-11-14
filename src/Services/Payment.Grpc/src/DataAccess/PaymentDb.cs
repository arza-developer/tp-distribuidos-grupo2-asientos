using Microsoft.EntityFrameworkCore;
using Payment.Grpc.Domain;

namespace Payment.Grpc.DataAccess;

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