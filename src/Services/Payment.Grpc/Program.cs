using Microsoft.EntityFrameworkCore;
using SeatsReservation.Contracts.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PaymentDb") ?? "Data Source=payment.db"));

builder.Services.AddGrpc();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.EnsureCreated();
}

app.MapGrpcService<PaymentService>();
app.MapGet("/", () => "Payment gRPC service running on port 5103");

app.Run();