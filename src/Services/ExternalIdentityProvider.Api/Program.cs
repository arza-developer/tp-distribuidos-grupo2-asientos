using ExternalIdentityProvider.Api.Services;

// [Simulación de 3rd Party Identity Provider]: hace de "Google".
// Servicio separado, con sus propios usuarios y su propia llave privada.
// El AuthServer NUNCA tiene esta llave privada: solo verifica con la pública (JWKS).

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IdTokenIssuer>();

// [Clase 6 - Razor Pages]: la página de login del proveedor (como la pantalla de Google).
builder.Services.AddRazorPages();

var app = builder.Build();

// Fail fast: si falta la configuración, explota al arrancar.
app.Services.GetRequiredService<IdTokenIssuer>();

app.MapRazorPages();

// [Estándar OIDC]: publicación de la llave pública (JWKS).
app.MapGet("/.well-known/jwks.json", (IdTokenIssuer idp) => Results.Ok(idp.GetJwks()));

// [Para pruebas por API]: login directo con un POST (pedidos 11 y 15 del .http).
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

// [Solo desarrollo]: hace de "aplicación" para probar la página de login sin el AuthServer.
// Muestra lo que llega por form_post. En el paso 2 la vuelta real va a ser /external/callback del AuthServer.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/test/callback", async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        return Results.Content(
            "<h1>Vuelta a la aplicación (prueba)</h1>" +
            $"<p><b>state:</b> {System.Net.WebUtility.HtmlEncode(form["state"].ToString())}</p>" +
            $"<p><b>id_token:</b></p><textarea rows='8' cols='100' readonly>{System.Net.WebUtility.HtmlEncode(form["id_token"].ToString())}</textarea>",
            "text/html");
    });
}

app.Run();