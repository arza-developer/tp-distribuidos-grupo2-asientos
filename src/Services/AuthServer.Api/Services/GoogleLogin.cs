namespace AuthServer.Api.Services;

// [3rd Party IdP - Google real]: nombres fijos del login con Google (igual que el ejemplo del docente con AddGoogle).
public static class GoogleLogin
{
    // El handler de Google (paquete Microsoft.AspNetCore.Authentication.Google).
    public const string Scheme = "Google";

    // Cookie temporal donde el handler deja a la persona ya identificada por Google.
    public const string CookieScheme = "External";

    // Dirección de vuelta registrada en Google Cloud Console: http://localhost:5104/signin-google
    public const string CallbackPath = "/signin-google";
}

// Si hay credenciales de Google cargadas, el login con Google está activo.
// Si no (por ejemplo en otra compu sin user-secrets), se sigue usando el proveedor simulado.
public sealed record GoogleLoginSettings(bool Enabled);
