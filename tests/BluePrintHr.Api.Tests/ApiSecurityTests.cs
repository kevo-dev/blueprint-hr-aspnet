using System.Net;
using BluePrintHr.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BluePrintHr.Api.Tests;

public class PasswordServiceTests
{
    [Fact]
    public void Password_hash_verifies_and_does_not_store_plaintext()
    {
        var service = new PasswordService();
        const string password = "Strong!Password123";
        var hash = service.Hash(password);
        Assert.NotEqual(password, hash);
        Assert.True(service.Verify(password, hash));
        Assert.False(service.Verify("Wrong!Password123", hash));
    }
}

public class ApiSecurityTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;
    public ApiSecurityTests(ApiFactory factory) => client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Health_is_public()
    {
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Mutating_api_requests_reject_untrusted_origin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent("{\"email\":\"x@example.com\",\"password\":\"bad\"}", System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var response = await client.GetAsync("/health");
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:UseInMemory"] = "true",
                ["Database:SeedDemoData"] = "false",
                ["Database:ApplyMigrations"] = "false",
                ["Auth:RequireHttps"] = "false",
                ["Auth:CookieSameSite"] = "Lax",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
            });
        });
    }
}
