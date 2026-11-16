using System.Text.RegularExpressions;
using Availability.Infrastructure.Auth;

namespace Availability.Tests.Auth;

public class PkceGeneratorTests
{
    [Fact]
    public void Challenge_MatchesTheRfc7636TestVector() =>
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            PkceGenerator.CreateChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void Verifier_Has43UnreservedBase64UrlChars() =>
        Assert.Matches(new Regex("^[A-Za-z0-9_-]{43}$"), PkceGenerator.CreateVerifier());
}
