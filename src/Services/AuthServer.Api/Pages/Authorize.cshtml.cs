using AuthServer.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuthServer.Api.Pages;

// [Clase 12 - Authorization Code + PKCE]: la "ventanilla" de la boletería.
// El front redirige al usuario acá. El usuario se identifica en NUESTRA página:
// el front nunca ve la contraseña.
public class AuthorizeModel : PageModel
{
    private readonly TokenService _tokens;

    public AuthorizeModel(TokenService tokens) => _tokens = tokens;

    // Parámetros OAuth2 que manda el front en la dirección (query string).
    // SupportsGet = se leen al abrir la página. En el formulario viajan como campos ocultos.
    [BindProperty(SupportsGet = true, Name = "response_type")] public string ResponseType { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "client_id")] public string ClientId { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "redirect_uri")] public string RedirectUri { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "code_challenge")] public string CodeChallenge { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "code_challenge_method")] public string CodeChallengeMethod { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "state")] public string State { get; set; } = "";

    // Lo que escribe el usuario en el formulario.
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";

    public string? LoginError { get; set; }      // usuario o contraseña incorrectos: se muestra y puede reintentar
    public string? InvalidRequest { get; set; }  // el pedido del front está mal: no se muestra el formulario

    // Al ABRIR la página: revisar el pedido del front y mostrar el formulario.
    public IActionResult OnGet() => ValidateRequest() ? Page() : BadRequestPage();

    // Al MANDAR el formulario.
    public IActionResult OnPost()
    {
        // [Seguridad]: se revisa de nuevo. Los campos ocultos se pueden modificar desde el navegador.
        if (!ValidateRequest()) return BadRequestPage();

        var user = _tokens.FindUser(Username, Password);
        if (user is null)
        {
            LoginError = "Usuario o contraseña incorrectos.";
            return Page();
        }

        // [Paso B3]: acá vamos a crear el código y redirigir al front.
        return Content($"Login correcto: {user.Username}. Falta el paso B3 (código y redirección).");
    }

    private bool ValidateRequest()
    {
        if (ResponseType != "code")
        {
            InvalidRequest = "response_type tiene que ser 'code'.";
            return false;
        }

        // [Seguridad]: solo clientes registrados y SOLO a sus direcciones registradas.
        // Si devolviéramos el código a cualquier dirección, un atacante podría pedir que se lo mandemos a él.
        if (_tokens.FindPublicClient(ClientId, RedirectUri) is null)
        {
            InvalidRequest = "Cliente o redirect_uri no registrados.";
            return false;
        }

        // [PKCE obligatorio]: sin la huella del talón no se puede verificar después quién canjea el código.
        if (string.IsNullOrEmpty(CodeChallenge) || CodeChallengeMethod != "S256")
        {
            InvalidRequest = "PKCE obligatorio: code_challenge con code_challenge_method=S256.";
            return false;
        }

        return true;
    }

    private IActionResult BadRequestPage()
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return Page();
    }
}