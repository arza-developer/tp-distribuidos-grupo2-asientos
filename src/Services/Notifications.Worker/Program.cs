using Notifications.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// 1. Configurar Serilog para leer desde la configuración (appsettings.json)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// 2. Reemplazar el logging por defecto de Microsoft por Serilog
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();