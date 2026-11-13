using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Coordin.Api.Features.Auth;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Resultado de um login bem-sucedido nos testes.</summary>
public sealed record TestSession(string AccessToken, string RefreshToken, string SetCookie);

/// <summary>
/// Auxiliares para os testes de integração entrarem como o gestor e usarem o cookie do refresh
/// token. Como o cookie é <c>Secure</c> e os testes falam em <c>http</c>, o cookie é lido do
/// <c>Set-Cookie</c> e reenviado à mão, sem o <c>CookieContainer</c>.
/// </summary>
public static class AuthTestClient
{
    public static Task<HttpResponseMessage> PostLoginAsync(this HttpClient client, string? email, string? password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    /// <summary>Entra com as credenciais de teste do gestor e exige sucesso.</summary>
    public static async Task<TestSession> LoginAsync(
        this HttpClient client,
        string email = TestManager.Email,
        string password = TestManager.Password)
    {
        var response = await client.PostLoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var setCookie = SetCookieHeader(response);
        Assert.NotNull(setCookie);

        return new TestSession(
            body.GetProperty("accessToken").GetString()!,
            CookieValue(setCookie),
            setCookie);
    }

    /// <summary>Cliente já autenticado como o gestor, com o token de acesso no cabeçalho.</summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this CoordinApiFactory factory)
    {
        var client = factory.CreateClient();
        var session = await client.LoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    /// <summary>O <c>Set-Cookie</c> do refresh token, ou nulo se a resposta não o definiu.</summary>
    public static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(value => value.StartsWith($"{RefreshTokenCookie.Name}=", StringComparison.Ordinal))
            : null;

    /// <summary>O valor do cookie (entre o primeiro <c>=</c> e o primeiro <c>;</c>).</summary>
    public static string CookieValue(string setCookie)
    {
        var start = setCookie.IndexOf('=', StringComparison.Ordinal) + 1;
        var end = setCookie.IndexOf(';', StringComparison.Ordinal);
        return end < 0 ? setCookie[start..] : setCookie[start..end];
    }

    public static HttpRequestMessage WithRefreshCookie(this HttpRequestMessage request, string refreshToken)
    {
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={refreshToken}");
        return request;
    }

    public static HttpRequestMessage WithBearer(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
