using System.Net;
using System.Text.Json;
using Availability.Infrastructure.Persistence;

namespace Availability.Tests.Api;

/// <summary>Contrato público de GET /api/seats (idéntico al de Availability.Api actual).</summary>
public class SeatsContractTests : IClassFixture<AvailabilityApiFactory>
{
    private readonly HttpClient _client;

    public SeatsContractTests(AvailabilityApiFactory factory) => _client = factory.CreateClient();

    private static async Task<JsonElement> ReadArrayAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task GetSeats_Anonymous_ReturnsMockCatalogInCamelCase()
    {
        var response = await _client.GetAsync("/api/seats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await ReadArrayAsync(response)).EnumerateArray().ToList();
        Assert.Equal(4, items.Count);

        Assert.All(items, i => Assert.Equal(
            ["seatNumber", "price", "isAvailable"], i.EnumerateObject().Select(p => p.Name)));

        var expected = new (string Number, decimal Price, bool Available)[]
        {
            ("A-1", 15000m, true), ("A-2", 15000m, true), ("A-3", 18000m, false), ("B-1", 12000m, true)
        };
        for (var n = 0; n < expected.Length; n++)
        {
            Assert.Equal(expected[n].Number, items[n].GetProperty("seatNumber").GetString());
            Assert.Equal(expected[n].Price, items[n].GetProperty("price").GetDecimal());
            Assert.Equal(expected[n].Available, items[n].GetProperty("isAvailable").GetBoolean());
        }
    }

    [Fact]
    public async Task GetSeats_KnownEventId_ReturnsSeatsOfThatEvent()
    {
        var response = await _client.GetAsync($"/api/seats?eventId={SeedData.EventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, (await ReadArrayAsync(response)).GetArrayLength());
    }

    [Fact]
    public async Task GetSeats_UnknownEventId_ReturnsEmptyArray()
    {
        var response = await _client.GetAsync($"/api/seats?eventId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await ReadArrayAsync(response)).GetArrayLength());
    }

    [Fact]
    public async Task GetSeats_MalformedEventId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/seats?eventId=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
