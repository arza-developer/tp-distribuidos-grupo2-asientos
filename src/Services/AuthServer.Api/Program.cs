using AuthServer.Api.Options;
using AuthServer.Api.Services;
using System.Security.Claims;
using SeatsReservation.Auth;

var builder = WebApplication.CreateBuilder(args);

// Registro de servicios: le decimos a .NET qué piezas existen.
builder.Services.Configure<AuthServerOptions>(builder.Configuration.GetSection("AuthServer"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

// Fail fast: creamos TokenService al arrancar, así si falta la clave explota acá y no en el primer pedido.
app.Services.GetRequiredService<TokenService>();
app.UseAuthentication();
app.UseAuthorization();

// Paso 2 de la consigna y requisito 3.e: emisión de tokens (OAuth2 Client Credentials)
app.MapPost("/connect/token", async (HttpRequest request, TokenService tokens) => // agregamos TokenService como parámetro, .NET lo inyecta automáticamente
{
    // OAuth2 dice que el pedido va como formulario, no como JSON.
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "invalid_request" });

    var form = await request.ReadFormAsync();

    if (form["grant_type"] != "client_credentials")
        return Results.BadRequest(new { error = "unsupported_grant_type" });

    var client = tokens.FindClient(form["client_id"].ToString(), form["client_secret"].ToString());
    if (client is null)
        return Results.Json(new { error = "invalid_client" }, statusCode: 401);

    var jwt = tokens.CreateToken(client.ClientId, client.Role);
    return Results.Ok(new { access_token = jwt, token_type = "Bearer", expires_in = TokenService.ExpiresInSeconds });
});

app.MapGet("/", () => "Auth Server running on port 5104. Use POST /connect/token to get JWT.");

// Criterio de aceptación: endpoint protegido. Sin token -> 401. Con token válido -> 200 y muestra sus claims.
app.MapGet("/connect/whoami", (ClaimsPrincipal user) =>
    Results.Ok(user.Claims.Select(c => new { c.Type, c.Value })))
    .RequireAuthorization();

app.Run();