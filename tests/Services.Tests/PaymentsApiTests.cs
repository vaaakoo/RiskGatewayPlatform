extern alias PaymentsAlias;

using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

using PaymentsProgram = PaymentsAlias::Program;

namespace Services.Tests;

public class PaymentsApiTests : IClassFixture<PaymentsApiTests.PaymentsTestFixture>
{
    private readonly HttpClient _client;
    private readonly RSA _rsa;

    public PaymentsApiTests(PaymentsTestFixture fixture)
    {
        _client = fixture.Client;
        _rsa = fixture.Rsa;
    }

    [Fact]
    public async Task No_token_returns_401()
    {
        var res = await _client.GetAsync("/payments");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_scope_returns_403()
    {
        var token = CreateToken("test-client", "orders.read");
        var req = new HttpRequestMessage(HttpMethod.Get, "/payments");
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Correct_scope_returns_200()
    {
        var token = CreateToken("test-client", "payments.read");
        var req = new HttpRequestMessage(HttpMethod.Get, "/payments");
        req.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var res = await _client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private string CreateToken(string clientId, string scope)
    {
        var key = new RsaSecurityKey(_rsa) { KeyId = "test-kid" };
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim("sub", clientId),
            new Claim("client_id", clientId),
            new Claim("scope", scope),
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

    public class PaymentsTestFixture : IDisposable
    {
        public HttpClient Client { get; }
        public RSA Rsa { get; }
        private readonly WebApplicationFactory<PaymentsProgram> _factory;

        public PaymentsTestFixture()
        {
            Rsa = RSA.Create(2048);
            var rsaKey = new RsaSecurityKey(Rsa) { KeyId = "test-kid" };

            _factory = new WebApplicationFactory<PaymentsProgram>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Testing");
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
            Rsa.Dispose();
        }
    }
}
