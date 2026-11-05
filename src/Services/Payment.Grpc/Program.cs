using Microsoft.EntityFrameworkCore;
using Payment.Grpc.Business;
using SeatsReservation.Auth;
using Payment.Grpc.DataAccess;

var builder = WebApplication.CreateBuilder(args);

// Configuración de Kestrel para HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// Agregar servicios al contenedor
builder.Services.AddGrpc();

// Base de datos SQLite
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PaymentDb")));

// Agregar autenticación JWT desde SeatsReservation.Auth
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

// Crear base de datos si no existe
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.EnsureCreated();
}

// Middlewares de Autenticación y Autorización en orden correcto
app.UseAuthentication();
app.UseAuthorization();

// Mapear servicio gRPC requiriendo autorización obligatoria
app.MapGrpcService<PaymentService>().RequireAuthorization();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();