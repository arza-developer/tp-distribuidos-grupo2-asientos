using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Availability.Web.Extensions;

/// <summary>
/// Autenticación a NIVEL GENERAL para convivir API y páginas: un <c>PolicyScheme</c> ("Smart") es el esquema por defecto
/// (autenticar, desafiar y prohibir) y reenvía por ruta: <c>/api/**</c> → JWT Bearer; todo lo demás → Cookie.
/// Así ningún controller ni action declara <c>AuthenticationSchemes</c>: alcanza con <c>[Authorize(Roles="Client")]</c> o
/// <c>[AllowAnonymous]</c>. Una cookie no autentica la API (401 JSON) y un Bearer no abre sesión en las páginas (redirige a login).
/// <para>
/// La librería compartida (<c>SeatsReservation.Auth</c>) solo registra Bearer y lo deja como esquema por defecto: la política por ruta se define aquí.
/// </para>
/// </summary>
public static class CookieAuthenticationExtensions
{
    public const string CookieScheme = "Cookies";
    public const string SmartScheme = "Smart";
    public const string CookieName = ".Availability.Session";
    public const string LoginPath = "/Account/Login";
    public const string AccessDeniedPath = "/Account/Denied";

    public static IServiceCollection AddCookieAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddPolicyScheme(SmartScheme, "Bearer en /api, Cookie en el resto", options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments("/api")
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // el laboratorio corre en HTTP (limitación M7)
                options.LoginPath = LoginPath;
                options.AccessDeniedPath = AccessDeniedPath;
                options.SlidingExpiration = false; // la sesión dura lo que el JWT (1 h), sin renovación
            });

        // AddJwtAuthentication (librería compartida) deja JwtBearer como esquema por defecto; se redefine aquí
        // (Configure se ejecuta después y gana).
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = SmartScheme;
            options.DefaultAuthenticateScheme = SmartScheme;
            options.DefaultChallengeScheme = SmartScheme;
            options.DefaultForbidScheme = SmartScheme;
        });

        return services;
    }
}
