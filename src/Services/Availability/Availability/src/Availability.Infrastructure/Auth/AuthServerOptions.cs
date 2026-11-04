using Microsoft.AspNetCore.DataProtection;

namespace Availability.Infrastructure.Auth;

/// <summary>
/// Credenciales y ubicación del Auth Server para el token M2M (Client Credentials) que Availability usa al llamar a Booking.
/// Sección <c>AuthServer</c>. El secreto NO va en <c>appsettings.json</c>: solo en Development o por variable de entorno
/// (<c>AuthServer__ClientSecret</c>).
/// </summary>
public class AuthServerOptions
{
    public const string SectionName = "AuthServer";
    public const string HttpClientName = "auth-server";

    /// <summary>En contenedores: http://auth-server:5104.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5104";

    /// <summary>Cliente registrado en el Auth Server (rama feature/06-auth-server-m2m: rol Service).</summary>
    public string ClientId { get; set; } = "availability";

    public string ClientSecret { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Se renueva el token este tiempo antes de que venza, para no usar uno a punto de expirar.</summary>
    public int TokenSkewSeconds { get; set; } = 60;

    // ---- Login federado por el IdP externo (Authorization Code + PKCE; Fase 8) ----

    /// <summary>
    /// URL del Auth Server ALCANZABLE POR EL NAVEGADOR (distinta de <see cref="BaseUrl"/> en contenedores, donde esa es
    /// interna): a ella se redirige a la persona para iniciar sesión (<c>/connect/authorize</c>).
    /// </summary>
    public string PublicUrl { get; set; } = "http://localhost:5104";

    /// <summary>Cliente público registrado en el Auth Server para las páginas (sin secreto, PKCE obligatorio).</summary>
    public string FrontendClientId { get; set; } = "frontend";

    /// <summary>
    /// Dirección de vuelta del código. Debe coincidir EXACTAMENTE con una de las <c>RedirectUris</c> del cliente en el Auth Server;
    /// se fija por configuración (no se deriva del header Host).
    /// </summary>
    public string FrontendRedirectUri { get; set; } = "http://localhost:5101/Account/Callback";
}
