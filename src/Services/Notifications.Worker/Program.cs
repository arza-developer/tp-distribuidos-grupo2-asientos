
using Azure.Messaging.ServiceBus;
using Notifications.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configurar Serilog desde appsettings.json.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// Reemplazar el logging predeterminado por Serilog.
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

var connectionString = builder.Configuration["AzureServiceBus:ConnectionString"]
    ?? throw new InvalidOperationException("Falta AzureServiceBus:ConnectionString");

var topicName = builder.Configuration["AzureServiceBus:TopicName"]
    ?? throw new InvalidOperationException("Falta AzureServiceBus:TopicName");

var subscriptionName = builder.Configuration["AzureServiceBus:SubscriptionName"]
    ?? throw new InvalidOperationException("Falta AzureServiceBus:SubscriptionName");

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(connectionString));

builder.Services.AddSingleton(new ServiceBusWorkerOptions
{
    TopicName = topicName,
    SubscriptionName = subscriptionName
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

public sealed class ServiceBusWorkerOptions
{
    public string TopicName { get; init; } = string.Empty;
    public string SubscriptionName { get; init; } = string.Empty;
}
