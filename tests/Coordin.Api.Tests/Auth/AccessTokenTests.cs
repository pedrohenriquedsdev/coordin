using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Coordin.Api.Features.Auth;
using Coordin.Api.Infrastructure.Auth;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coordin.Api.Tests.Auth;

/// <summary>
/// Spec authentication: token de acesso e proteção de todos os endpoints por padrão (DT-28, RNF-04).
/// <c>/api/_test/protected</c> não tem nenhuma regra própria: se ele exige login, qualquer endpoint
/// de negócio futuro também exigirá.
/// </summary>
public class AccessTokenTests(TestingApiFactory factory) : IClassFixture<TestingApiFactory>
{
    private const string ProtectedPath = "/api/_test/protected";

    private readonly HttpClient _client = factory.CreateClient();

    private JwtOptions Jwt => factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

    [Fact]
    public async Task TokenService_IssuesTheClaimsAndLifetimeOfTheTdd()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(TestManager.Email))!;

        var token = factory.Services.GetRequiredService<TokenService>().CreateAccessToken(user);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);
        Assert.Equal(user.Id, jwt.Subject);
        Assert.Equal(TestManager.Email, jwt.GetClaim("email").Value);
        Assert.Equal(Jwt.Issuer, jwt.Issuer);
        Assert.Equal(Jwt.Audience, Assert.Single(jwt.Audiences));
        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.ValidFrom);
    }

    [Fact]
    public async Task ValidToken_IsAccepted()
    {
        var token = await IssueRealTokenAsync();

        var response = await GetAsync(ProtectedPath, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401_EvenWhenItExpiredJustASecondAgo()
    {
        // Sem a tolerância de 5 minutos que o padrão da biblioteca concederia.
        var token = TestTokens.Create(Jwt, expires: DateTime.UtcNow.AddSeconds(-1));

        var response = await GetAsync(ProtectedPath, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Returns401()
    {
        var token = TestTokens.Create(Jwt, signingKey: new string('x', 48));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ProtectedPath, token)).StatusCode);
    }

    [Fact]
    public async Task TokenFromAnotherIssuer_Returns401()
    {
        var token = TestTokens.Create(Jwt, issuer: "outro-emissor");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ProtectedPath, token)).StatusCode);
    }

    [Fact]
    public async Task TokenForAnotherAudience_Returns401()
    {
        var token = TestTokens.Create(Jwt, audience: "outra-audiencia");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ProtectedPath, token)).StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherAlgorithm_Returns401()
    {
        // Mesma chave, mas HS512: a API só aceita o algoritmo que ela própria emite.
        var token = TestTokens.Create(Jwt, algorithm: SecurityAlgorithms.HmacSha512);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ProtectedPath, token)).StatusCode);
    }

    [Fact]
    public async Task TamperedPayload_Returns401()
    {
        var parts = (await IssueRealTokenAsync()).Split('.');
        var payload = parts[1].ToCharArray();
        payload[5] = payload[5] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}.{new string(payload)}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ProtectedPath, tampered)).StatusCode);
    }

    [Theory]
    [InlineData("nao-e-um-token")]
    [InlineData("a.b.c")]
    [InlineData("")]
    public async Task MalformedToken_Returns401(string token)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(ProtectedPath, token)).StatusCode);
    }

    [Fact]
    public async Task EndpointWithoutAnyRule_WithoutToken_Returns401ProblemDetails()
    {
        var response = await _client.GetAsync(ProtectedPath, TestContext.Current.CancellationToken);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Bearer");

        using var body = JsonDocument.Parse(text);
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Não autenticado", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("É necessário entrar para acessar este recurso.", body.RootElement.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ok\"", text, StringComparison.Ordinal); // nenhum dado do endpoint
    }

    [Fact]
    public async Task Health_StaysPublic()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownApiRoute_WithoutToken_Returns404_NotUnauthorized()
    {
        var response = await _client.GetAsync("/api/rota-inexistente", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DiagnosticEndpoints_RemainAnonymous()
    {
        var response = await _client.GetAsync("/api/_test/json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SpaHomePage_RemainsPublic_ButItCarriesNoData()
    {
        var response = await _client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> IssueRealTokenAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(TestManager.Email))!;
        return factory.Services.GetRequiredService<TokenService>().CreateAccessToken(user);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
