using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExternalIdentityProvider.Api.Services;

// Lo que sabe hacer el proveedor externo (el "Google" simulado):
// verificar a SUS usuarios y firmar id_tokens con SU llave privada (RS256).
// [Singleton]: una sola instancia, así la llave es la misma para el login y para el JWKS.
public sealed class IdTokenIssuer
{
    // [RS256]: par de llaves. La privada firma (nunca sale de acá); la pública se publica en el JWKS.
    // [Decisión TP]: se genera al arrancar; si el proveedor se reinicia, cambia el par de llaves.
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _keyId = Guid.NewGuid().ToString("N");

    private readonly string _issuer;
    private readonly List<ExternalUser> _users;
    private readonly List<RegisteredClient> _clients;

    public IdTokenIssuer(IConfiguration configuration)
    {
        _issuer = configuration["Issuer"]
            ?? throw new InvalidOperationException("Falta 'Issuer' en la configuración del proveedor");
        _users = configuration.GetSection("Users").Get<List<ExternalUser>>() ?? new();
        _clients = configuration.GetSection("Clients").Get<List<RegisteredClient>>() ?? new();
    }

    // ¿Existe esta persona en el proveedor y la contraseña es correcta?
    public ExternalUser? FindUser(string email, string password)
    {
        var user = _users.FirstOrDefault(u => u.Email == email);
        return user is not null && user.Password == password ? user : null;
    }

    // [Seguridad]: solo aplicaciones registradas, y solo a SUS direcciones de vuelta.
    public RegisteredClient? FindClient(string clientId, string redirectUri)
    {
        var client = _clients.FirstOrDefault(c => c.ClientId == clientId);
        return client is not null && client.RedirectUris.Contains(redirectUri) ? client : null;
    }

    // [Estándar OIDC]: id_token = "esta persona es Fulano", firmado con la llave PRIVADA.
    // audience = para qué aplicación es. nonce = valor que mandó esa aplicación, para evitar que lo reusen.
    public string CreateIdToken(ExternalUser user, string audience, string? nonce)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Subject),   // id de la persona EN EL PROVEEDOR
            new("email", user.Email),
            new("name", user.Name)
        };
        if (!string.IsNullOrEmpty(nonce)) claims.Add(new Claim("nonce", nonce));

        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = audience,
            IssuedAt = now,
            Expires = now.AddMinutes(5), // corto: solo sirve para entregárselo a la aplicación
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(_rsa) { KeyId = _keyId }, SecurityAlgorithms.RsaSha256)
        });
    }

    // [Estándar OIDC]: la llave PÚBLICA en formato JWKS. ExportParameters(false) = sin la parte privada.
    public object GetJwks()
    {
        var publicKey = new RsaSecurityKey(_rsa.ExportParameters(false)) { KeyId = _keyId };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
        return new
        {
            keys = new[] { new { kty = jwk.Kty, use = "sig", alg = "RS256", kid = jwk.Kid, n = jwk.N, e = jwk.E } }
        };
    }
}

// [Dominio Anémico]: usuario del proveedor. Solo datos.
public record ExternalUser(string Email, string Password, string Subject, string Name);

// [Dominio Anémico]: aplicación registrada en el proveedor (ej. nuestro AuthServer).
public sealed class RegisteredClient
{
    public string ClientId { get; set; } = string.Empty;
    public List<string> RedirectUris { get; set; } = new();
}