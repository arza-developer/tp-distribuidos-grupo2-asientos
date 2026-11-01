using SeatsReservation.Contracts.Events;
using SeatsReservation.Contracts.Messaging;
using SeatsReservation.Observability; // <-- Namespace de tu librería de observabilidad
using Serilog; // <-- Namespace de Serilog

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Serilog para leer desde appsettings.json y capturar logs estructurados
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Registrar los servicios de observabilidad transversales (LogContext, etc.)
builder.Services.AddSeatsObservability();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registramos el publisher mock para poder trabajar offline sin broker real
builder.Services.AddSingleton<IMessagePublisher, FakeLoggingPublisher>();

var app = builder.Build();

// 3. Registrar el Middleware de Correlación al comienzo absoluto del pipeline HTTP
app.UseSeatsObservability();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();