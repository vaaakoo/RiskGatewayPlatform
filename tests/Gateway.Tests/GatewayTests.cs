using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Gateway.Tests;

public class GatewayTests : IClassFixture<GatewayTests.GatewayTestFixture>
{
    private readonly HttpClient _client;
    private readonly RSA _rsa;

    public GatewayTests(GatewayTestFixture fixture)
    {
        _client = fixture.Client;
        _rsa = fixture.Rsa;
    }

    [Fact]
    public async Task Missing_token_returns_401()
    {
        var res = await _client.GetAsync("/orders");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_scope_returns_403()
    {
        var token = CreateToken("test-client", "payments.read");
        var req = new HttpRequestMessage(HttpMethod.Get, "/orders");
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rate_limit_returns_429_after_enough_calls()
    {
        // Use "strict" policy = 10 requests / 10s window
        var token = CreateToken("rate-limit-test-client", "orders.read", rateLimitPolicy: "strict");
        int rateLimitedCount = 0;

        for (int i = 0; i < 20; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/orders");
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var res = await _client.SendAsync(req);

            if (res.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedCount++;
            }
        }

        rateLimitedCount.Should().BeGreaterThan(0, "strict policy (10 req/10s) should reject some of 20 requests");
    }

    private string CreateToken(string clientId, string scope, string rateLimitPolicy = "standard")
    {
        var key = new RsaSecurityKey(_rsa) { KeyId = "test-kid" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim("sub", clientId),
            new Claim("client_id", clientId),
            new Claim("scope", scope),
            new Claim("rate_limit_policy", rateLimitPolicy),
        };

        var jwt = new JwtSecurityToken(
            issuer: "identity",
            audience: "gateway",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public class GatewayTestFixture : IDisposable
    {
        public HttpClient Client { get; }
        public RSA Rsa { get; }

        private readonly WebApplicationFactory<Program> _factory;
        private readonly WebApplication _fakeDownstream;

        public GatewayTestFixture()
        {
            // Start a fake downstream API on a random port
            var downstreamBuilder = WebApplication.CreateBuilder();
            downstreamBuilder.WebHost.UseUrls("http://127.0.0.1:0");
            _fakeDownstream = downstreamBuilder.Build();
            _fakeDownstream.MapGet("/orders", () => Microsoft.AspNetCore.Http.Results.Ok(new[] { new { id = 1, item = "demo" } }));
            _fakeDownstream.MapPost("/orders", () => Microsoft.AspNetCore.Http.Results.Created("/orders/1", new { id = 1 }));
            _fakeDownstream.MapGet("/payments", () => Microsoft.AspNetCore.Http.Results.Ok(new[] { new { id = 1, amount = 100.0m } }));
            _fakeDownstream.MapPost("/payments", () => Microsoft.AspNetCore.Http.Results.Created("/payments/1", new { id = 1 }));
            _fakeDownstream.StartAsync().GetAwaiter().GetResult();

            var downstreamUrl = _fakeDownstream.Urls.First();

            Rsa = RSA.Create(2048);
            var rsaKey = new RsaSecurityKey(Rsa) { KeyId = "test-kid" };

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
                    builder.UseSetting("ReverseProxy:Clusters:orders:Destinations:d1:Address", downstreamUrl + "/");
                    builder.UseSetting("ReverseProxy:Clusters:payments:Destinations:d1:Address", downstreamUrl + "/");
                    builder.ConfigureServices(services =>
                    {
                        services.PostConfigure<JwtBearerOptions>(
                            JwtBearerDefaults.AuthenticationScheme,
                            o =>
                            {
                                o.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateIssuer = true,
                                    ValidIssuer = "identity",
                                    ValidateAudience = true,
                                    ValidAudience = "gateway",
                                    ValidateLifetime = true,
                                    ValidateIssuerSigningKey = true,
                                    IssuerSigningKey = rsaKey,
                                };
                                o.Events = new JwtBearerEvents();
                            });
                    });
                });

            Client = _factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
            _fakeDownstream.StopAsync().GetAwaiter().GetResult();
            _fakeDownstream.DisposeAsync().GetAwaiter().GetResult();
            Rsa.Dispose();
        }
    }
}
