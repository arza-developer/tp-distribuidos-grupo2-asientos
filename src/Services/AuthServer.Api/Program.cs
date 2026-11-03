using System.Security.Claims;
using AuthServer.Api.Options;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.WebUtilities;
using SeatsReservation.Auth;

var builder = WebApplication.CreateBuilder(args);

// Registro de servicios: le decimos a .NET qué piezas existen.
builder.Services.Configure<AuthServerOptions>(builder.Configuration.GetSection("AuthServer"));
builder.Services.Configure<ExternalIdpOptions>(builder.Configuration.GetSection("ExternalIdp"));
builder.Services.AddSingleton<TokenService>();
// [Authorization Code]: registro de comprobantes. Singleton: UNO solo para todo el AuthServer,
// así un código creado en un pedido se encuentra en el siguiente.
builder.Services.AddSingleton<AuthorizationCodeStore>();
// [3rd Party IdP]: logins externos en curso (mismo motivo: singleton).
builder.Services.AddSingleton<ExternalLoginStore>();
builder.Services.AddJwtAuthentication(builder.Configuration);

// [3rd Party IdP]: cliente HTTP para descargar el JWKS del proveedor externo.
builder.Services.AddHttpClient<ExternalTokenValidator>(c => c.Timeout = TimeSpan.FromSeconds(5));

// [Clase 6 - Razor Pages]: la página de login de la boletería (Authorization Code).
builder.Services.AddRazorPages();

var app = builder.Build();

// Fail fast: creamos TokenService al arrancar, así si falta la clave explota acá y no en el primer pedido.
app.Services.GetRequiredService<TokenService>();

// [Clase 12]: primero autenticación (¿quién sos?), después autorización (¿qué podés hacer?).
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Paso 2 de la consigna y requisito 3.e: emisión de tokens (OAuth2)
app.MapPost("/connect/token", async (HttpRequest request, TokenService tokens, ExternalTokenValidator external, AuthorizationCodeStore codes) =>
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

    // Ventanilla 3: canje directo del id_token del proveedor externo (para pruebas por API).
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

    // Ventanilla 4: el front canjea el comprobante de retiro (Authorization Code + PKCE, Clase 12).
    if (grantType == "authorization_code")
    {
        // Se saca del registro AUNQUE después falle algo: un código sirve una sola vez, para bien o para mal.
        var entry = codes.Redeem(form["code"].ToString());
        if (entry is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "código inválido, vencido o ya usado" });

        // Lo tiene que canjear la MISMA app, con la MISMA dirección de vuelta que lo pidió.
        if (entry.ClientId != form["client_id"].ToString() || entry.RedirectUri != form["redirect_uri"].ToString())
            return Results.BadRequest(new { error = "invalid_grant", error_description = "client_id o redirect_uri no coinciden" });

        // [PKCE]: ¿encaja el talón? Si alguien robó el código, no tiene el code_verifier.
        if (!AuthorizationCodeStore.VerifyPkce(form["code_verifier"].ToString(), entry.CodeChallenge))
            return Results.BadRequest(new { error = "invalid_grant", error_description = "code_verifier no coincide (PKCE)" });

        var jwt = tokens.CreateToken(entry.UserId, entry.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    return Results.BadRequest(new { error = "unsupported_grant_type" });
});

// [3rd Party IdP - vuelta del proveedor]: el proveedor externo devuelve acá el id_token (OIDC form_post).
// Si todo está bien, seguimos el login del front como siempre: código de un solo uso + PKCE.
app.MapPost("/external/callback", async (HttpRequest request, ExternalLoginStore externalLogins,
    ExternalTokenValidator external, TokenService tokens, AuthorizationCodeStore codes) =>
{
    if (!request.HasFormContentType)
        return ErrorPage("Pedido inválido.");

    var form = await request.ReadFormAsync();

    // 1. ¿Es la vuelta de un login que NOSOTROS empezamos? (state de un solo uso)
    var pending = externalLogins.Take(form["state"].ToString());
    if (pending is null)
        return ErrorPage("El inicio de sesión venció o no es válido. Volvé a intentarlo.");

    // 2. ¿El id_token es auténtico, es para nosotros y trae NUESTRO nonce?
    var externalSub = await external.ValidateAsync(form["id_token"].ToString(), pending.Nonce);
    if (externalSub is null)
        return ErrorPage("No se pudo verificar tu identidad con el proveedor externo.");

    // 3. ¿Esa persona está registrada en nuestra boletería?
    var user = tokens.FindUserByExternalSubject(externalSub);
    if (user is null)
        return ErrorPage("Tu cuenta del proveedor externo no está registrada en la Boletería.");

    // 4. Igual que el login normal: comprobante para la app, que lo canjea con su talón (PKCE).
    var code = codes.Create(pending.ClientId, pending.RedirectUri, pending.CodeChallenge, user.UserId, user.Role);
    var callbackUrl = QueryHelpers.AddQueryString(pending.RedirectUri, new Dictionary<string, string?>
    {
        ["code"] = code,
        ["state"] = pending.FrontState
    });
    return Results.Redirect(callbackUrl);
});

app.MapGet("/", () => "Auth Server running on port 5104. Use POST /connect/token to get JWT.");

// Criterio de aceptación: endpoint protegido. Sin token -> 401. Con token válido -> 200 y muestra sus claims.
app.MapGet("/connect/whoami", (ClaimsPrincipal user) =>
    Results.Ok(user.Claims.Select(c => new { c.Type, c.Value })))
    .RequireAuthorization();

// [Solo desarrollo]: hace de "app del recital" para probar sin front.
// Muestra el código que devolvió la boletería. Cuando exista el front, su dirección reemplaza a esta.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/test/callback", (string? code, string? state) =>
        Results.Content(
            $"<h1>Vuelta a la app (prueba)</h1>" +
            $"<p><b>code:</b> {System.Net.WebUtility.HtmlEncode(code)}</p>" +
            $"<p><b>state:</b> {System.Net.WebUtility.HtmlEncode(state)}</p>",
            "text/html"));
}

app.Run();

// Página de error simple para la vuelta del proveedor (la persona la ve en el navegador).
static IResult ErrorPage(string message) => Results.Content(
    $"<h1>Boletería · No se pudo ingresar</h1><p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
    "text/html", statusCode: 400);