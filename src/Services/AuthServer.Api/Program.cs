using System.Security.Claims;
using AuthServer.Api.Options;
using AuthServer.Api.Services;
using Microsoft.AspNetCore.WebUtilities;
using SeatsReservation.Auth;
using Serilog;
using SeatsReservation.Observability;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar Serilog para leer desde appsettings.json y capturar logs estructurados
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 2. Registrar los servicios de observabilidad transversales (LogContext, etc.)
builder.Services.AddSeatsObservability();

// Registro de servicios: le decimos a .NET qué piezas existen.
builder.Services.Configure<AuthServerOptions>(builder.Configuration.GetSection("AuthServer"));
builder.Services.Configure<ExternalIdpOptions>(builder.Configuration.GetSection("ExternalIdp"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<AuthorizationCodeStore>();
builder.Services.AddSingleton<ExternalLoginStore>();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddHttpClient<ExternalTokenValidator>(c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddRazorPages();

var app = builder.Build();

// 3. Registrar el Middleware de Correlación al comienzo absoluto del pipeline HTTP
app.UseSeatsObservability();

app.Services.GetRequiredService<TokenService>();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// Endpoint de emisión de tokens OAuth2 unificado
app.MapPost("/connect/token", async (HttpRequest request, TokenService tokens, ExternalTokenValidator external, AuthorizationCodeStore codes) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "invalid_request" });

    var form = await request.ReadFormAsync();
    var grantType = form["grant_type"].ToString();

    // Ventanilla 1: microservicios (Client Credentials, requisito 3.e)
    if (grantType == "client_credentials")
    {
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();

        var client = tokens.FindClient(clientId, clientSecret);
        if (client is null)
            return Results.Json(new { error = "invalid_client" }, statusCode: 401);

        Serilog.Log.Information("INICIO: Emitiendo token JWT para el cliente {ClientId}", clientId);

        // Un único emisor de tokens (TokenService): misma clave Jwt:Key que validan todos los servicios, mismo issuer/audience y vida.
        // (Una firma distinta aquí dejaría sin validar los tokens M2M: ningún servicio los aceptaría.)
        var jwt = tokens.CreateToken(client.ClientId, client.Role);

        Serilog.Log.Information("FIN: Token JWT emitido exitosamente para {ClientId}", clientId);

        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    // Ventanilla 2: personas con usuario local (Password Grant)
    if (grantType == "password")
    {
        var user = tokens.FindUser(form["username"].ToString(), form["password"].ToString());
        if (user is null)
            return Results.BadRequest(new { error = "invalid_grant" });

        var jwt = tokens.CreateToken(user.UserId, user.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    // Ventanilla 3: canje directo del id_token del proveedor externo
    if (grantType == "urn:ietf:params:oauth:grant-type:token-exchange")
    {
        var externalSub = await external.ValidateAsync(form["subject_token"].ToString());
        if (externalSub is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "id_token externo inválido o vencido" });

        var user = tokens.FindUserByExternalSubject(externalSub);
        if (user is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "usuario externo no registrado en el sistema" });

        var jwt = tokens.CreateToken(user.UserId, user.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    // Ventanilla 4: el front canjea el comprobante de retiro (Authorization Code + PKCE)
    if (grantType == "authorization_code")
    {
        var entry = codes.Redeem(form["code"].ToString());
        if (entry is null)
            return Results.BadRequest(new { error = "invalid_grant", error_description = "código inválido, vencido o ya usado" });

        if (entry.ClientId != form["client_id"].ToString() || entry.RedirectUri != form["redirect_uri"].ToString())
            return Results.BadRequest(new { error = "invalid_grant", error_description = "client_id o redirect_uri no coinciden" });

        if (!AuthorizationCodeStore.VerifyPkce(form["code_verifier"].ToString(), entry.CodeChallenge))
            return Results.BadRequest(new { error = "invalid_grant", error_description = "code_verifier no coincide (PKCE)" });

        var jwt = tokens.CreateToken(entry.UserId, entry.Role);
        return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
    }

    return Results.BadRequest(new { error = "unsupported_grant_type" });
})
.WithName("GetToken")
.WithSummary("[Paso 2 y 3.e]: Emisión de tokens de autenticación JWT")
.WithDescription("Genera tokens simétricos HS256 tanto para usuarios finales como para comunicación servicio a servicio (M2M).");

// [3rd Party IdP - vuelta del proveedor]
app.MapPost("/external/callback", async (HttpRequest request, ExternalLoginStore externalLogins,
    ExternalTokenValidator external, TokenService tokens, AuthorizationCodeStore codes) =>
{
    if (!request.HasFormContentType)
        return ErrorPage("Pedido inválido.");

    var form = await request.ReadFormAsync();

    var pending = externalLogins.Take(form["state"].ToString());
    if (pending is null)
        return ErrorPage("El inicio de sesión venció o no es válido. Volvé a intentarlo.");

    var externalSub = await external.ValidateAsync(form["id_token"].ToString(), pending.Nonce);
    if (externalSub is null)
        return ErrorPage("No se pudo verificar tu identidad con el proveedor externo.");

    var user = tokens.FindUserByExternalSubject(externalSub);
    if (user is null)
        return ErrorPage("Tu cuenta del proveedor externo no está registrada en la Boletería.");

    var code = codes.Create(pending.ClientId, pending.RedirectUri, pending.CodeChallenge, user.UserId, user.Role);
    var callbackUrl = QueryHelpers.AddQueryString(pending.RedirectUri, new Dictionary<string, string?>
    {
        ["code"] = code,
        ["state"] = pending.FrontState
    });
    return Results.Redirect(callbackUrl);
});

app.MapGet("/", () => "Auth Server running on port 5104. Use POST /connect/token to get JWT.");

// Endpoint protegido de prueba
app.MapGet("/connect/whoami", (ClaimsPrincipal user) =>
    Results.Ok(user.Claims.Select(c => new { c.Type, c.Value })))
    .RequireAuthorization();

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

static IResult ErrorPage(string message) => Results.Content(
    $"<h1>Boletería · No se pudo ingresar</h1><p>{System.Net.WebUtility.HtmlEncode(message)}</p>",
    "text/html", statusCode: 400);

public record TokenRequestDto(string? ClientId, string? ClientSecret, string? Role);