using Serilog;
using SeatsReservation.Observability; // <-- Namespace de tu librería de observabilidad

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Serilog para leer desde appsettings.json y capturar logs estructurados
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Registrar los servicios de observabilidad transversales (LogContext, etc.)
builder.Services.AddSeatsObservability();

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Registrar el Middleware de Correlación al comienzo absoluto del pipeline HTTP
app.UseSeatsObservability();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();