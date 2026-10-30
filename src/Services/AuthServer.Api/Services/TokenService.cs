using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
    public RegisteredClient? FindClient(string clientId, string clientSecret)
    {
        var client = _registry.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (client is null) return null;
        if (client.ClientSecret != clientSecret) return null;
        return client;
    }

    // Busca el usuario (persona) en la lista. Si no existe o la contraseña no coincide, devuelve null.
    public DemoUser? FindUser(string username, string password)
    {
        var user = _registry.Users.FirstOrDefault(u => u.Username == username);
        if (user is null) return null;
        if (user.Password != password) return null;
        return user;
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