using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Infrastructure.Auth;
using Availability.Web.Extensions;
using Availability.Web.Mvc.Models;
using Availability.Web.Mvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Availability.Web.Mvc.Controllers;

/// <summary>
/// Sesión de las páginas: login por contraseña (grant password, solo pruebas), login federado por el IdP externo
/// (Authorization Code + PKCE vía el Auth Server, Fase 8), logout y una página de perfil.
/// La cookie se elige a nivel general (ver <see cref="CookieAuthenticationExtensions"/>): aquí no se declara esquema.
/// </summary>
public class AccountController(
    SessionLoginService login,
    ExternalLoginFlow externalFlow,
    IOptions<AuthServerOptions> authOptions,
    ICorrelationIdAccessor correlation) : MvcControllerBase
{
    private const string GenericExternalError = "No se pudo completar el ingreso con el proveedor externo. Intentá nuevamente.";
    private const string AuthServerDownMessage = "El servicio de autenticación no está disponible. Intentá nuevamente en unos minutos.";

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        var password = model.Password;
        model.Password = string.Empty; // nunca se devuelve a la vista; solo se usa para este intento

        if (!ModelState.IsValid) return View(model);

        LoginResult result;
        try
        {
            result = await login.LoginAsync(model.Username, password, correlation.CorrelationId, ct);
        }
        catch (AuthServerUnavailableException)
        {
            ModelState.AddModelError(string.Empty, AuthServerDownMessage);
            return View(model);
        }

        return await CompleteLoginAsync(result, model, "Usuario o contraseña incorrectos.");
    }

    /// <summary>
    /// Inicia el login federado: guarda state + PKCE en la cookie de flujo y redirige al navegador al Auth Server
    /// (<c>/connect/authorize?...&amp;idp_hint=external</c>). POST con antiforgery (lo exige la base de los controllers de páginas).
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public IActionResult LoginExternal(string? returnUrl = null) =>
        Redirect(externalFlow.Start(Response, Url.IsLocalUrl(returnUrl) ? returnUrl : null));

    /// <summary>
    /// Vuelta del Auth Server con <c>code</c> y <c>state</c>. Valida el state contra la cookie de flujo, canjea el código por
    /// canal trasero y abre la misma sesión que el login por contraseña. Ningún error revela detalles del Auth Server ni del IdP.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(string? code, string? state, string? error, CancellationToken ct)
    {
        var flow = externalFlow.Consume(Request, Response, state); // siempre borra la cookie
        var model = new LoginViewModel();

        if (flow is null || !string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError(string.Empty, "El inicio de sesión venció o no es válido. Volvé a intentarlo.");
            return View(nameof(Login), model);
        }

        model.ReturnUrl = flow.Value.ReturnUrl;
        var options = authOptions.Value;
        LoginResult result;
        try
        {
            result = await login.LoginWithCodeAsync(code, flow.Value.Verifier, options.FrontendRedirectUri,
                options.FrontendClientId, correlation.CorrelationId, ct);
        }
        catch (AuthServerUnavailableException)
        {
            ModelState.AddModelError(string.Empty, AuthServerDownMessage);
            return View(nameof(Login), model);
        }

        return await CompleteLoginAsync(result, model, GenericExternalError);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationExtensions.CookieScheme);
        TempData["Info"] = "Cerraste sesión.";
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Se llega aquí cuando hay sesión pero el rol no alcanza (el esquema de cookie redirige a esta ruta).</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    /// <summary>Muestra con qué identidad opera la sesión (también sirve para comprobar que la cookie funciona).</summary>
    [HttpGet]
    [Authorize(Roles = SessionLoginService.RequiredRole)]
    public IActionResult Profile() => View();

    /// <summary>Resultado común de ambos logins: abre la cookie y redirige, o vuelve al formulario con un mensaje genérico.</summary>
    private async Task<IActionResult> CompleteLoginAsync(LoginResult result, LoginViewModel model, string invalidMessage)
    {
        switch (result.Status)
        {
            case LoginStatus.Success:
                await HttpContext.SignInAsync(CookieAuthenticationExtensions.CookieScheme, result.Principal!,
                    new AuthenticationProperties { ExpiresUtc = result.ExpiresUtc, IsPersistent = false, AllowRefresh = false });
                return Redirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");

            case LoginStatus.RoleNotAllowed:
                ModelState.AddModelError(string.Empty, "Tu cuenta no tiene permiso para reservar asientos desde esta página.");
                return View(nameof(Login), model);

            default: // InvalidCredentials, InvalidToken o InvalidCode: mismo mensaje, no se distingue hacia afuera
                ModelState.AddModelError(string.Empty, invalidMessage);
                return View(nameof(Login), model);
        }
    }
}
