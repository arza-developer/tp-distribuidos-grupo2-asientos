using System.Security.Cryptography;
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

app.MapGet("/", () => "External Identity Provider (simulado) en el puerto 5105");

app.Run();