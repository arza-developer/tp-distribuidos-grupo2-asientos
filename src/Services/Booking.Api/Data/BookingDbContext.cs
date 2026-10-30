using Booking.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Booking.Api.Data;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Reservation>(entity =>
        {
            // Identificador primario de la reserva 
            entity.HasKey(r => r.Id);
            // Indice unico para el RequestId
            entity.HasIndex(r => r.RequestId).IsUnique();
            // Conversion de enum a int para el estado de la reserva
            entity.Property(r => r.Status).HasConversion<int>();
            // Evitamos dos reservas para el mismo asiento en el mismo evento, solo si el estado es Retained o Confirmed
            entity.HasIndex(r => new {r.EventId,r.SeatNumber}).IsUnique().HasFilter("\"Status\" IN (1, 2)");
            entity.Property(r => r.SeatNumber).IsRequired().HasMaxLength(20);
        });
    }
}