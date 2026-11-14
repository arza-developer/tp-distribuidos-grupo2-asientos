using Microsoft.AspNetCore.Mvc;

namespace Availability.Web.Mvc;

/// <summary>
/// Base de los controllers de páginas (ControllerWithViews). Centraliza lo que aplica a TODAS las páginas:
/// antiforgery automático en los métodos no seguros (POST) y exclusión de Swagger. NO declara esquema de autenticación:
/// lo resuelve la política general (<c>CookieAuthenticationExtensions</c>).
/// </summary>
[AutoValidateAntiforgeryToken]
[ApiExplorerSettings(IgnoreApi = true)]
public abstract class MvcControllerBase : Controller;
