using System.Security.Claims;
using Availability.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Availability.Web.Mvc.Services;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    RoleNotAllowed,
    InvalidToken
}

/// <param name="Principal">Identidad para la cookie (solo con <see cref="LoginStatus.Success"/>).</param>
/// <param name="ExpiresUtc">Vencimiento del JWT: la sesión dura lo mismo.</param>
public sealed record LoginResult(LoginStatus Status, ClaimsPrincipal? Principal = null, DateTimeOffset? ExpiresUtc = null);

/// <summary>
/// Login de las páginas: pide el token al Auth Server (password grant), lo VALIDA con los mismos parámetros que la API
/// (firma, issuer, audience, vigencia), exige el rol <c>Client</c> y un <c>sub</c> Guid, y arma la identidad de la cookie
/// con solo <c>sub</c>, <c>role</c> y <c>name</c>. El JWT no se conserva.
/// </summary>
public sealed class SessionLoginService(
    IAuthServerLoginClient loginClient,
    IOptionsMonitor<JwtBearerOptions> jwtOptions,
    ILogger<SessionLoginService> logger)
{
    public const string RequiredRole = "Client";
    private const int MaxNameLength = 100;

    public async Task<LoginResult> LoginAsync(string username, string password, string correlationId, CancellationToken ct = default)
    {
        var token = await loginClient.LoginAsync(username, password, correlationId, ct);
        if (token is null) return new LoginResult(LoginStatus.InvalidCredentials);

        var parameters = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken jwt)
        {
            logger.LogWarning("El token devuelto por el Auth Server en el login no es válido ({Reason}).",
                validation.Exception?.GetType().Name ?? "desconocido");
            return new LoginResult(LoginStatus.InvalidToken);
        }

        var sub = validation.ClaimsIdentity.FindFirst("sub")?.Value;
        var role = validation.ClaimsIdentity.FindFirst("role")?.Value;
        if (role != RequiredRole || !Guid.TryParse(sub, out _))
        {
            logger.LogInformation("Login rechazado: el token del usuario {Username} no es de un usuario con rol {Role}.",
                username, RequiredRole);
            return new LoginResult(LoginStatus.RoleNotAllowed);
        }

        var name = username.Length > MaxNameLength ? username[..MaxNameLength] : username;
        var identity = new ClaimsIdentity(
            [new Claim("sub", sub!), new Claim("role", role), new Claim("name", name)],
            authenticationType: "Cookies", nameType: "name", roleType: "role");

        return new LoginResult(LoginStatus.Success, new ClaimsPrincipal(identity), new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
    }
}
