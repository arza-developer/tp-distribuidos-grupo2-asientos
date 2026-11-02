using ExternalIdentityProvider.Api.Services;

// [Simulación de 3rd Party Identity Provider]: hace de "Google".
// Servicio separado, con sus propios usuarios y su propia llave privada.
// El AuthServer NUNCA tiene esta llave privada: solo verifica con la pública (JWKS).

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IdTokenIssuer>();

var app = builder.Build();

// Fail fast: si falta la configuración, explota al arrancar.
app.Services.GetRequiredService<IdTokenIssuer>();

// [Estándar OIDC]: publicación de la llave pública (JWKS).
app.MapGet("/.well-known/jwks.json", (IdTokenIssuer idp) => Results.Ok(idp.GetJwks()));

// [Para pruebas por API]: login directo con un POST (pedidos 11 y 15 del .http).
// La página de login (como Google) la agregamos en el paso 1b.
app.MapPost("/login", async (HttpRequest request, IdTokenIssuer idp, IConfiguration config) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "invalid_request" });

    var form = await request.ReadFormAsync();
    var user = idp.FindUser(form["username"].ToString(), form["password"].ToString());
    if (user is null)
        return Results.Json(new { error = "invalid_credentials" }, statusCode: 401);

    var idToken = idp.CreateIdToken(user, config["Audience"]!, nonce: null);
    return Results.Ok(new { id_token = idToken, expires_in = 300 });
});

app.MapGet("/", () => "External Identity Provider (simulado) en el puerto 5105");

app.Run();