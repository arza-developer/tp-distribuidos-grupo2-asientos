using ExternalIdentityProvider.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExternalIdentityProvider.Api.Pages;

// [Simulación de "Iniciar sesión con Google"]: la página de login DEL PROVEEDOR.
// La aplicación (nuestro AuthServer) manda acá al usuario. El usuario se identifica acá,
// y el proveedor lo devuelve a la aplicación con un id_token firmado.
// [Estándar OIDC - response_mode=form_post]: el id_token vuelve en un formulario POST,
// no en la barra de direcciones (no queda en el historial).
public class AuthorizeModel : PageModel
{
    private readonly IdTokenIssuer _idp;

    public AuthorizeModel(IdTokenIssuer idp) => _idp = idp;

    // Parámetros que manda la aplicación en la dirección.
    [BindProperty(SupportsGet = true, Name = "response_type")] public string ResponseType { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "client_id")] public string ClientId { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "redirect_uri")] public string RedirectUri { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "state")] public string State { get; set; } = "";
    [BindProperty(SupportsGet = true, Name = "nonce")] public string Nonce { get; set; } = "";

    // Lo que escribe el usuario.
    [BindProperty] public string Email { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";

    public string? LoginError { get; set; }      // mail o contraseña incorrectos: puede reintentar
    public string? InvalidRequest { get; set; }  // el pedido de la aplicación está mal: no se muestra el formulario
    public string? IdToken { get; set; }         // login correcto: lo que vuelve a la aplicación

    public IActionResult OnGet() => ValidateRequest() ? Page() : BadRequestPage();

    public IActionResult OnPost()
    {
        // [Seguridad]: se revisa de nuevo. Los campos ocultos se pueden modificar desde el navegador.
        if (!ValidateRequest()) return BadRequestPage();

        var user = _idp.FindUser(Email, Password);
        if (user is null)
        {
            LoginError = "Email o contraseña incorrectos.";
            return Page();
        }

        // El "aud" del id_token es la aplicación que lo pidió (client_id). El nonce vuelve adentro del token.
        IdToken = _idp.CreateIdToken(user, ClientId, Nonce);
        return Page(); // la página arma el formulario oculto que vuelve solo a la aplicación
    }

    private bool ValidateRequest()
    {
        if (ResponseType != "id_token")
        {
            InvalidRequest = "response_type tiene que ser 'id_token'.";
            return false;
        }

        // [Seguridad]: solo aplicaciones registradas y SOLO a sus direcciones de vuelta.
        // Si no, alguien podría pedir que le mandemos el id_token de otra persona a su página.
        if (_idp.FindClient(ClientId, RedirectUri) is null)
        {
            InvalidRequest = "Aplicación o redirect_uri no registradas.";
            return false;
        }

        // state: la aplicación reconoce su pedido. nonce: evita que se reuse un id_token viejo.
        if (string.IsNullOrEmpty(State) || string.IsNullOrEmpty(Nonce))
        {
            InvalidRequest = "Faltan state o nonce.";
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
