namespace AuthServer.Api.Options;

// Tiene la misma forma que la sección "AuthServer" del appsettings.json
public sealed class AuthServerOptions
{
    public List<RegisteredClient> Clients { get; set; } = new();
    public List<DemoUser> Users { get; set; } = new();
}

// Un microservicio autorizado a pedir tokens
public sealed class RegisteredClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// Un usuario de prueba (persona)
public sealed class DemoUser
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}