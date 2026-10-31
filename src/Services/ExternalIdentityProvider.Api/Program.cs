using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// [Simulación de 3rd Party Identity Provider]: hace de "Google".
// Servicio separado, con sus propios usuarios y su propia clave privada.
// El AuthServer NUNCA tiene esta clave privada: solo verifica con la pública (JWKS).

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// [RS256]: par de llaves. La privada firma (solo la tiene este servicio).
// La pública verifica (se publica para que otros chequeen los tokens).
// [Decisión TP]: se genera al arrancar; si el proveedor se reinicia, cambia el par de llaves.
var rsa = RSA.Create(2048);
var keyId = Guid.NewGuid().ToString("N"); // "kid": nombre de esta llave, para encontrarla en el JWKS

// La llave PRIVADA, envuelta para firmar. Nunca se publica.
var privateKey = new RsaSecurityKey(rsa) { KeyId = keyId };

// Configuración y usuarios propios del proveedor (no son los del AuthServer).
var issuer = builder.Configuration["Issuer"]!;
var audience = builder.Configuration["Audience"]!;
var users = builder.Configuration.GetSection("Users").Get<List<ExternalUser>>() ?? new();

// [Estándar OIDC]: publicación de la llave pública (JWKS).
app.MapGet("/.well-known/jwks.json", () =>
{
    // ExportParameters(false) = SOLO la parte pública. La privada no sale nunca de acá.
    var publicKey = new RsaSecurityKey(rsa.ExportParameters(false)) { KeyId = keyId };
    var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);

    return Results.Ok(new
    {
        keys = new[]
        {
            new { kty = jwk.Kty, use = "sig", alg = "RS256", kid = jwk.Kid, n = jwk.N, e = jwk.E }
        }
    });
});

// [Simplificación TP]: en un proveedor real, acá habría una PÁGINA de login (Razor/MVC)
// y una redirección del navegador (Authorization Code Flow). Lo reducimos a un POST
// para poder probarlo con el archivo .http.
app.MapPost("/login", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "invalid_request" });

    var form = await request.ReadFormAsync();
    var user = users.FirstOrDefault(u => u.Email == form["username"].ToString());
    if (user is null || user.Password != form["password"].ToString())
        return Results.Json(new { error = "invalid_credentials" }, statusCode: 401);

    // [Estándar OIDC]: id_token = "esta persona es Fulano", firmado con la llave PRIVADA (RS256).
    var now = DateTime.UtcNow;
    var idToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim("sub", user.Subject),   // id de la persona EN EL PROVEEDOR
            new Claim("email", user.Email),
            new Claim("name", user.Name)
        }),
        Issuer = issuer,                     // "iss": lo emitió este proveedor
        Audience = audience,                 // "aud": es para el AuthServer
        IssuedAt = now,
        Expires = now.AddMinutes(5),         // corto: solo sirve para canjearlo en el AuthServer
        SigningCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256)
    });

    return Results.Ok(new { id_token = idToken, expires_in = 300 });
});

app.MapGet("/", () => "External Identity Provider (simulado) en el puerto 5105");

app.Run();

// [Dominio Anémico]: usuario del proveedor externo. Solo datos, sin lógica.
public record ExternalUser(string Email, string Password, string Subject, string Name);