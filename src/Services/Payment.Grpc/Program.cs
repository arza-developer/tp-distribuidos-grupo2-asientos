using Payment.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<PaymentService>();
app.MapGet("/", () => "Payment gRPC service running on port 5103");

app.Run();
