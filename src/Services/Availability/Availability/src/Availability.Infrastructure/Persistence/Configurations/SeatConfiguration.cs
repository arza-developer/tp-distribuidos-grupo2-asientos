using Availability.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Availability.Infrastructure.Persistence.Configurations;

public class SeatConfiguration : IEntityTypeConfiguration<Seat>
{
    public void Configure(EntityTypeBuilder<Seat> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();

        builder.Property(s => s.SeatId).IsRequired();
        builder.HasIndex(s => s.SeatId).IsUnique();

        builder.Property(s => s.SeatNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(s => new { s.EventId, s.SeatNumber }).IsUnique();

        // SQLite no tiene tipo decimal nativo: EF lo guarda como TEXT (suficiente para lectura/escritura simple).
        builder.Property(s => s.Price).HasPrecision(18, 2);
    }
}
