using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthServer.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SeatsReservation.Auth;

namespace AuthServer.Api.Services;

// Capa de negocio: verifica credenciales y fabrica los tokens.
public sealed class TokenService
{
    public const int ExpiresInSeconds = 3600; // 1 hora

    private readonly AuthServerOptions _registry;
    private readonly string _secretKey;

    // .NET llama a este constructor y le pasa la lista (IOptions) y la configuración.
    public TokenService(IOptions<AuthServerOptions> registry, IConfiguration configuration)
    {
        _registry = registry.Value;
        _secretKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Falta Jwt:Key en la configuración");
    }

    // Busca el cliente (microservicio) en la lista. Si no existe o el secreto no coincide, devuelve null.
    // Busca un microservicio (cliente CON secreto). Si no existe o el secreto no coincide, devuelve null.
    public RegisteredClient? FindClient(string clientId, string clientSecret)
    {
        var client = _registry.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (client is null) return null;

        // [Seguridad]: un cliente público (sin secreto) NO puede usar Client Credentials.
        // Sin este chequeo, mandar client_secret vacío ("") coincidiría.
        if (string.IsNullOrEmpty(client.ClientSecret)) return null;

        if (client.ClientSecret != clientSecret) return null;
        return client;
    }

    // [Authorization Code]: busca un cliente público (el front) y verifica que la dirección de vuelta esté permitida.
    public RegisteredClient? FindPublicClient(string clientId, string redirectUri)
    {
        var client = _registry.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (client is null) return null;
        if (!string.IsNullOrEmpty(client.ClientSecret)) return null; // los que tienen secreto son microservicios
        if (!client.RedirectUris.Contains(redirectUri)) return null;  // solo direcciones registradas
        return client;
    }

    // Busca el usuario (persona) en la lista. Si no existe o la contraseña no coincide, devuelve null.
    public DemoUser? FindUser(string username, string password)
    {
        var user = _registry.Users.FirstOrDefault(u => u.Username == username);
        if (user is null) return null;

        // [Seguridad]: un usuario sin contraseña local solo entra por el proveedor externo.
        // Sin este chequeo, mandar password vacío ("") coincidiría y entraría cualquiera.
        if (string.IsNullOrEmpty(user.Password)) return null;

        if (user.Password != password) return null;
        return user;
    }

    // [3rd Party IdP]: busca qué usuario nuestro está vinculado a ese "sub" del proveedor externo.
    public DemoUser? FindUserByExternalSubject(string externalSubject) =>
        _registry.Users.FirstOrDefault(u => u.ExternalSubject == externalSubject);

    // [3rd Party IdP - Google]: primero busca si la cuenta está vinculada en la lista (ExternalSubject = "google:<sub>").
    // Si no, la registra sola con rol Client (auto-provisioning, como el ejemplo del docente con IdentityServer).
    // El UserId sale del SHA256 de "google:<sub>": la misma cuenta de Google tiene siempre el mismo UserId,
    // aunque el AuthServer se reinicie, sin necesidad de guardarla en ningún lado.
    public DemoUser ResolveExternalUser(string provider, string providerSubject)
    {
        var externalSubject = $"{provider}:{providerSubject}";

        var linked = FindUserByExternalSubject(externalSubject);
        if (linked is not null) return linked;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(externalSubject));
        return new DemoUser
        {
            Username = externalSubject,
            UserId = new Guid(hash.AsSpan(0, 16)).ToString(),
            Role = "Client",
            ExternalSubject = externalSubject
        };
    }

    // Arma el JWT. El rol viene de la lista, NUNCA de lo que mandó el que pide.
    public string CreateToken(string subject, string role)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", role)
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddSeconds(ExpiresInSeconds),
            Issuer = JwtAuthenticationExtensions.Issuer,
            Audience = JwtAuthenticationExtensions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}