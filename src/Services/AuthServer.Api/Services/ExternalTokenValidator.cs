using AuthServer.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AuthServer.Api.Services;

// [3rd Party IdP]: verifica los id_token del proveedor externo (RS256) con su clave PÚBLICA.
// El AuthServer no tiene la clave privada del proveedor: puede verificar sus tokens, pero no fabricarlos.
public sealed class ExternalTokenValidator
{
    private readonly HttpClient _http;
    private readonly ExternalIdpOptions _options;
    private readonly ILogger<ExternalTokenValidator> _logger;

    public ExternalTokenValidator(HttpClient http, IOptions<ExternalIdpOptions> options, ILogger<ExternalTokenValidator> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    // Devuelve el "sub" del proveedor si el id_token es válido; null si no lo es.
    public async Task<string?> ValidateAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;

        // 1. Descargar las llaves públicas del proveedor (JWKS).
        // [Decisión TP]: se descargan en cada canje. Un login es poco frecuente; en producción se cachearían.
        string jwksJson;
        try
        {
            jwksJson = await _http.GetStringAsync($"{_options.Authority}/.well-known/jwks.json");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "[AUTH] No se pudo contactar al proveedor externo en {Authority}", _options.Authority);
            return null;
        }
        var publicKeys = new JsonWebKeySet(jwksJson).GetSigningKeys();

        // 2. Verificar el id_token: las mismas preguntas que hace nuestra librería, pero con la llave de OTRO.
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,          // ¿lo emitió el proveedor en el que confiamos?
            ValidateAudience = true,
            ValidAudience = _options.Audience,      // ¿es para nosotros?
            ValidateLifetime = true,                // ¿está vencido?
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = publicKeys,         // ¿la firma la hizo la llave privada del proveedor?

            // [Seguridad]: solo aceptamos RS256. Evita que alguien mande un token firmado con otro algoritmo.
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
        });

        if (!result.IsValid)
        {
            _logger.LogWarning("[AUTH] id_token externo rechazado: {Reason}", result.Exception?.Message);
            return null;
        }

        return result.ClaimsIdentity.FindFirst("sub")?.Value;
    }
}