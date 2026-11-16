namespace Booking.Api.Business.Interfaces;

public interface IAuthenticationClient
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
}

public sealed class AuthenticationClientConfiguration
{
    public const string SectionName = "AuthServer";

    public string AuthenticationEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}