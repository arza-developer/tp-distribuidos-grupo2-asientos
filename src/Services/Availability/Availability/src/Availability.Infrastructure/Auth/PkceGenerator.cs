using System.Security.Cryptography;
using System.Text;

namespace Availability.Infrastructure.Auth;

/// <summary>
/// Valores del flujo Authorization Code + PKCE (RFC 7636) y del <c>state</c> anti-CSRF. Todo sale de un generador
/// criptográfico y se codifica en base64url sin relleno.
/// </summary>
public static class PkceGenerator
{
    /// <summary>Método de desafío que exige el Auth Server (<c>code_challenge_method=S256</c>).</summary>
    public const string ChallengeMethod = "S256";

    /// <summary>43 caracteres (32 bytes aleatorios): el mínimo que permite el RFC para un verifier.</summary>
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary><c>BASE64URL(SHA256(ASCII(verifier)))</c>.</summary>
    public static string CreateChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Valor opaco e impredecible para correlacionar la ida y la vuelta del navegador.</summary>
    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(24));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
