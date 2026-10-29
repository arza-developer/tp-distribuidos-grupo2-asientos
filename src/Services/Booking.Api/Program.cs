using Booking.Api.Clients;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SeatsReservation.Contracts.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddGrpcClient<PaymentApi.PaymentApiClient>(options =>
        options.Address = new Uri(builder.Configuration["Services:PaymentGrpc"]
            ?? throw new InvalidOperationException("Falta la configuración 'Services:PaymentGrpc'.")))
    .AddResilienceHandler("payment", pipeline =>
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = Polly.DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(200)
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15)
        });

        pipeline.AddTimeout(TimeSpan.FromSeconds(2));
    });

builder.Services.AddScoped<IPaymentClient, PaymentClient>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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