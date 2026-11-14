using System.Net;

namespace Availability.Tests.Api;

public class HealthTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly HttpClient _client;

    public HealthTests(AvailabilityApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }
}
