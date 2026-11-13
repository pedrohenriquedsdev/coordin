using System.Net;
using System.Text.Json;
using Coordin.Api.Infrastructure.Auth;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Tests.Auth;

/// <summary>Spec authentication: dados do usuário autenticado e ausência de cadastro público (DT-28, DT-29).</summary>
public class CurrentUserTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task WithAValidToken_ReturnsOnlyTheEmail()
    {
        var session = await _client.LoginAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me").WithBearer(session.AccessToken);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(text);
        var properties = body.RootElement.EnumerateObject().ToList();
        Assert.Equal("email", Assert.Single(properties).Name);
        Assert.Equal(TestManager.Email, properties[0].Value.GetString());
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithoutAToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ATokenOfAnAccountThatNoLongerExists_Returns401()
    {
        // Assinatura e validade corretas, mas o 'sub' não é de nenhum usuário do banco.
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var token = TestTokens.Create(jwt);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me").WithBearer(token);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/signup")]
    [InlineData("/api/register")]
    public async Task ThereIsNoPublicRegistration(string path)
    {
        var post = await _client.PostAsync(path, content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }
}
