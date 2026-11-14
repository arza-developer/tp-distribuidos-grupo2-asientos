using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Availability.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeatRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReservationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeatId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeatNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeatRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeatRequests_ReservationId",
                table: "SeatRequests",
                column: "ReservationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeatRequests");
        }
    }
}
