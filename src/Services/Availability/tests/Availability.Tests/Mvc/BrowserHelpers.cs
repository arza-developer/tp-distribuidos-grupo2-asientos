using System.Net;
using System.Text.RegularExpressions;

namespace Availability.Tests.Mvc;

/// <summary>Utilidades para manejar formularios con antiforgery desde los tests (sin navegador ni JavaScript).</summary>
public static partial class BrowserHelpers
{
    public const string SessionCookie = ".Availability.Session";

    // Busca el <input> oculto del token (el orden de los atributos puede variar) y captura su value.
    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenInputRegex();

    [GeneratedRegex("value=\"([^\"]+)\"")]
    private static partial Regex ValueRegex();

    /// <summary>Pide la página y devuelve el token antiforgery de su formulario (la cookie queda en el cliente).</summary>
    public static async Task<string> GetAntiforgeryTokenAsync(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var input = TokenInputRegex().Match(html);
        Assert.True(input.Success, $"No hay token antiforgery en {url}");
        var value = ValueRegex().Match(input.Value);
        Assert.True(value.Success, $"El input antiforgery de {url} no tiene value");
        return value.Groups[1].Value;
    }

    public static Task<HttpResponseMessage> PostFormAsync(
        this HttpClient client, string url, Dictionary<string, string> fields, string? antiforgeryToken)
    {
        var data = new Dictionary<string, string>(fields);
        if (antiforgeryToken is not null) data["__RequestVerificationToken"] = antiforgeryToken;
        return client.PostAsync(url, new FormUrlEncodedContent(data));
    }

    /// <summary>Inicia sesión en las páginas con las credenciales dadas (siguiendo el flujo real: GET login → POST con token).</summary>
    public static async Task<HttpResponseMessage> LoginAsync(
        this HttpClient client, string user = "cliente1", string password = "cliente1-dev", string? returnUrl = null)
    {
        var token = await client.GetAntiforgeryTokenAsync("/Account/Login");
        var fields = new Dictionary<string, string> { ["Username"] = user, ["Password"] = password };
        if (returnUrl is not null) fields["ReturnUrl"] = returnUrl;
        return await client.PostFormAsync("/Account/Login", fields, token);
    }

    public static IReadOnlyList<string> SetCookies(this HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];

    public static string? SessionSetCookie(this HttpResponseMessage response) =>
        response.SetCookies().FirstOrDefault(c => c.StartsWith(SessionCookie + "=", StringComparison.Ordinal));

    /// <summary>Destino de la redirección como ruta + query (la cookie de ASP.NET redirige con URL absoluta).</summary>
    public static string? RedirectLocation(this HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther)) return null;
        var location = response.Headers.Location;
        if (location is null) return null;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    /// <summary>HTML de la respuesta con las entidades decodificadas (Razor codifica los acentos: ñ → &amp;#xF1;).</summary>
    public static async Task<string> ReadHtmlAsync(this HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
}
