using System.Net.Sockets;

namespace Availability.Tests.Messaging;

/// <summary>Fact que se omite si Azurite (cola en 127.0.0.1:10001) no responde.</summary>
public sealed class AzuriteFactAttribute : FactAttribute
{
    public const string ConnectionString = "UseDevelopmentStorage=true";

    public AzuriteFactAttribute()
    {
        if (!IsAzuriteUp()) Skip = "Azurite no está disponible en 127.0.0.1:10001 (docker compose up azurite).";
    }

    private static bool IsAzuriteUp()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", 10001).Wait(TimeSpan.FromMilliseconds(500)) && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
