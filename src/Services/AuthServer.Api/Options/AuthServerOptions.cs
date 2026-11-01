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

// Un usuario (persona)
public sealed class DemoUser
{
    public string Username { get; set; } = string.Empty;

    // Vacío = este usuario NO puede entrar con contraseña local (solo por el proveedor externo).
    public string Password { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    // [3rd Party IdP]: vinculación con el "sub" del proveedor externo. null = no vinculado.
    public string? ExternalSubject { get; set; }
}

// Tiene la misma forma que la sección "ExternalIdp": en qué proveedor externo confiamos.
public sealed class ExternalIdpOptions
{
    public string Authority { get; set; } = string.Empty;  // dónde está (para descargar su JWKS)
    public string Issuer { get; set; } = string.Empty;     // "iss" que tienen que traer sus tokens
    public string Audience { get; set; } = string.Empty;   // "aud" que tienen que traer sus tokens
}