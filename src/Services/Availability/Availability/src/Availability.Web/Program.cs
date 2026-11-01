using Availability.Application;
using Availability.Application.Reservations;
using Availability.Infrastructure;
using Availability.Application.Abstractions;
using Availability.Infrastructure.Persistence;
using Availability.Web.Extensions;
using Availability.Web.Logging;
using Availability.Web.Middleware;
using Availability.Web.Services;
using Microsoft.OpenApi.Models;
using Serilog;
using SeatsReservation.Auth; // TD-6: hoy es la copia local de Web/_SharedAuthCopy; luego será la librería compartida

var builder = WebApplication.CreateBuilder(args);

// Requisito 3.f: logging estructurado con el CorrelationId de la request en cada línea.
// .ReadFrom.Services permite sumar sinks por DI (los tests capturan eventos así).
// preserveStaticLogger: cada host usa su propio logger y no pisa el Log.Logger estático (aísla hosts paralelos de test).
builder.Host.UseSerilog(preserveStaticLogger: true, configureLogger: (context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("CorrelationId", "-") // valor por defecto fuera de una request (LogContext tiene prioridad)
    .Filter.ByExcluding(LogFilters.IsHandledException)
    .WriteTo.Console(outputTemplate: "[{CorrelationId}] {Level:u3} {SourceContext}: {Message:l}{NewLine}{Exception}"));

builder.Services.AddApplication();
builder.Services.Configure<ReservationSyncOptions>(builder.Configuration.GetSection(ReservationSyncOptions.SectionName));
builder.Services.AddInfrastructure();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

// Requisito 3.e: autenticación con tokens del Auth Server (rol Client para las reservas; catálogo y /health públicos).
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthProblemDetails();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token emitido por el Auth Server (POST /connect/token)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

// El id de correlación va primero: así también lo llevan los logs del manejador de excepciones.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

// Migraciones + seed centralizados.
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

app.Run();

// Necesario para WebApplicationFactory<Program> en los tests de integración.
public partial class Program;
