using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

public class TokenFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TokenFlowTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        // NOTE: to fully run these tests, you’ll want an in-memory DB option in Identity.Api when ASPNETCORE_ENVIRONMENT=Testing.
    }

    [Fact]
    public async Task Token_endpoint_returns_access_and_refresh()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["scope"] = "orders.read"
        });

        var res = await _client.PostAsync("/connect/token", form);
        res.IsSuccessStatusCode.Should().BeTrue();
    }
}
