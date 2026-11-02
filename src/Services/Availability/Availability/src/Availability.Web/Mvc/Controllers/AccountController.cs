using Availability.Application.Abstractions;
using Availability.Application.Exceptions;
using Availability.Web.Extensions;
using Availability.Web.Mvc.Models;
using Availability.Web.Mvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Mvc.Controllers;

/// <summary>
/// Sesión de las páginas: login (password grant contra el Auth Server), logout y una página de perfil.
/// La cookie se elige a nivel general (ver <see cref="CookieAuthenticationExtensions"/>): aquí no se declara esquema.
/// </summary>
public class AccountController(SessionLoginService login, ICorrelationIdAccessor correlation) : MvcControllerBase
{
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
            ModelState.AddModelError(string.Empty, "El servicio de autenticación no está disponible. Intentá nuevamente en unos minutos.");
            return View(model);
        }

        switch (result.Status)
        {
            case LoginStatus.Success:
                await HttpContext.SignInAsync(CookieAuthenticationExtensions.CookieScheme, result.Principal!,
                    new AuthenticationProperties { ExpiresUtc = result.ExpiresUtc, IsPersistent = false, AllowRefresh = false });
                return Redirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/");

            case LoginStatus.RoleNotAllowed:
                ModelState.AddModelError(string.Empty, "Tu cuenta no tiene permiso para reservar asientos desde esta página.");
                return View(model);

            default: // InvalidCredentials o InvalidToken: mismo mensaje, no se distingue hacia afuera
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return View(model);
        }
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
}
