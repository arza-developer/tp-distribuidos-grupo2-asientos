using System.Security.Claims;
using AuthServer.Api.Options;
using AuthServer.Api.Services;
using SeatsReservation.Auth;

var builder = WebApplication.CreateBuilder(args);

// Registro de servicios: le decimos a .NET qué piezas existen.
builder.Services.Configure<AuthServerOptions>(builder.Configuration.GetSection("AuthServer"));
builder.Services.Configure<ExternalIdpOptions>(builder.Configuration.GetSection("ExternalIdp"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddJwtAuthentication(builder.Configuration);

// [3rd Party IdP]: cliente HTTP para descargar el JWKS del proveedor externo.
builder.Services.AddHttpClient<ExternalTokenValidator>(c => c.Timeout = TimeSpan.FromSeconds(5));

var app = builder.Build();

// Fail fast: creamos TokenService al arrancar, así si falta la clave explota acá y no en el primer pedido.
app.Services.GetRequiredService<TokenService>();

// [Clase 12]: primero autenticación (¿quién sos?), después autorización (¿qué podés hacer?).
app.UseAuthentication();
app.UseAuthorization();

// Paso 2 de la consigna y requisito 3.e: emisión de tokens (OAuth2)
app.MapPost("/connect/token", async (HttpRequest request, TokenService tokens, ExternalTokenValidator external) =>
{
    // OAuth2 dice que el pedido va como formulario, no como JSON.
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "invalid_request" });

    var form = await request.ReadFormAsync();
    var grantType = form["grant_type"].ToString();

    // Ventanilla 1: microservicios (Client Credentials, requisito 3.e)
    if (grantType == "client_credentials")
    {
        var client = tokens.FindClient(form["client_id"].ToString(), form["client_secret"].ToString());
        if (client is null)
            return Results.Json(new { error = "invalid_client" }, statusCode: 401);

        var jwt = tokens.CreateToken(client.ClientId, client.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    // Ventanilla 2: personas con usuario local (Password Grant).
    // [Simplificación TP]: este flujo no está entre los vigentes (Clase 12). Lo dejamos solo para pruebas.
    if (grantType == "password")
    {
        var user = tokens.FindUser(form["username"].ToString(), form["password"].ToString());
        if (user is null)
            return Results.BadRequest(new { error = "invalid_grant" });

        // El "sub" es el UserId: así Availability sabe quién reserva sin creerle al body.
        var jwt = tokens.CreateToken(user.UserId, user.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    // Ventanilla 3: personas que se loguearon en el proveedor externo (3rd Party IdP, federación).
    // [Estándar RFC 8693 - Token Exchange]: canjea el id_token del proveedor por NUESTRO access token.
    if (grantType == "urn:ietf:params:oauth:grant-type:token-exchange")
    {
        var externalSub = await external.ValidateAsync(form["subject_token"].ToString());
        if (externalSub is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "id_token externo inválido o vencido" });

        // El proveedor dice QUIÉN es. Nuestro sistema decide QUÉ puede hacer (UserId y rol propios).
        var user = tokens.FindUserByExternalSubject(externalSub);
        if (user is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "usuario externo no registrado en el sistema" });

        var jwt = tokens.CreateToken(user.UserId, user.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    return Results.BadRequest(new { error = "unsupported_grant_type" });
});

app.MapGet("/", () => "Auth Server running on port 5104. Use POST /connect/token to get JWT.");

// Criterio de aceptación: endpoint protegido. Sin token -> 401. Con token válido -> 200 y muestra sus claims.
app.MapGet("/connect/whoami", (ClaimsPrincipal user) =>
    Results.Ok(user.Claims.Select(c => new { c.Type, c.Value })))
    .RequireAuthorization();

app.Run();