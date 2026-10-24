using SeatsReservation.Contracts.Events;
using SeatsReservation.Contracts.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registramos el publisher mock para poder trabajar offline sin broker real
builder.Services.AddSingleton<IMessagePublisher, FakeLoggingPublisher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
