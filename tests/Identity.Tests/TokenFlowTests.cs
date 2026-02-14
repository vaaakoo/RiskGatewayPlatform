using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class TokenFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TokenFlowTests(WebApplicationFactory<Program> factory)
    {
        var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
        });
        _client = customFactory.CreateClient();
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
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("refresh_token").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("token_type").GetString().Should().Be("Bearer");
        json.GetProperty("scope").GetString().Should().Contain("orders.read");
    }

    [Fact]
    public async Task Refresh_rotation_returns_new_refresh_token()
    {
        // Step 1: get initial tokens
        var form1 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["scope"] = "orders.read"
        });
        var res1 = await _client.PostAsync("/connect/token", form1);
        var json1 = await res1.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken1 = json1.GetProperty("refresh_token").GetString()!;

        // Step 2: rotate
        var form2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["refresh_token"] = refreshToken1,
            ["scope"] = "orders.read"
        });
        var res2 = await _client.PostAsync("/connect/token", form2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        var json2 = await res2.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken2 = json2.GetProperty("refresh_token").GetString()!;

        refreshToken2.Should().NotBe(refreshToken1, "rotation must produce a new refresh token");
        json2.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Reuse_detection_revokes_session()
    {
        // Step 1: get initial tokens
        var form1 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["scope"] = "orders.read"
        });
        var res1 = await _client.PostAsync("/connect/token", form1);
        var json1 = await res1.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken1 = json1.GetProperty("refresh_token").GetString()!;

        // Step 2: rotate (legitimate)
        var form2 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["refresh_token"] = refreshToken1,
            ["scope"] = "orders.read"
        });
        var res2 = await _client.PostAsync("/connect/token", form2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var json2 = await res2.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken2 = json2.GetProperty("refresh_token").GetString()!;

        // Step 3: replay OLD refresh token -> reuse detected -> 401
        var form3 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["refresh_token"] = refreshToken1,
            ["scope"] = "orders.read"
        });
        var res3 = await _client.PostAsync("/connect/token", form3);
        res3.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "reuse of old token must be rejected");

        // Step 4: even the new token is now revoked (entire session killed)
        var form4 = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["refresh_token"] = refreshToken2,
            ["scope"] = "orders.read"
        });
        var res4 = await _client.PostAsync("/connect/token", form4);
        res4.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "session should be fully revoked");
    }

    [Fact]
    public async Task Invalid_client_returns_401()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "nonexistent",
            ["client_secret"] = "bad",
            ["scope"] = "orders.read"
        });

        var res = await _client.PostAsync("/connect/token", form);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Invalid_scope_returns_400()
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "test-client",
            ["client_secret"] = "test-secret",
            ["scope"] = "admin.nuke"
        });

        var res = await _client.PostAsync("/connect/token", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Jwks_endpoint_returns_key()
    {
        var res = await _client.GetAsync("/.well-known/jwks.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("keys").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var key = json.GetProperty("keys")[0];
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("alg").GetString().Should().Be("RS256");
    }
}
