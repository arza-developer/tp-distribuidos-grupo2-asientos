using Availability.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Availability.Infrastructure.Persistence.Configurations;

public class SeatRequestConfiguration : IEntityTypeConfiguration<SeatRequest>
{
    public void Configure(EntityTypeBuilder<SeatRequest> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedOnAdd();

        builder.Property(r => r.ReservationId).IsRequired();
        builder.HasIndex(r => r.ReservationId).IsUnique();

        builder.Property(r => r.SeatNumber).IsRequired().HasMaxLength(20);
        builder.Property(r => r.CorrelationId).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Price).HasPrecision(18, 2);
        builder.Property(r => r.Status).HasConversion<int>();
    }
}
