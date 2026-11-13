namespace Coordin.Api.Features.Auth;

/// <summary>
/// O cookie do refresh token (DT-28): <c>HttpOnly</c> (JavaScript não lê), <c>Secure</c> (só HTTPS),
/// <c>SameSite=Strict</c> (outro site não o envia) e restrito a <c>/api/auth</c>, para que o navegador
/// não o mande em nenhuma outra chamada.
/// </summary>
public static class RefreshTokenCookie
{
    public const string Name = "refresh_token";
    public const string Path = "/api/auth";

    public static void Append(HttpResponse response, IssuedRefreshToken token) =>
        response.Cookies.Append(Name, token.Value, Options(maxAge: RefreshTokenService.Lifetime));

    /// <summary>Para apagar, o navegador exige os mesmos atributos (principalmente o <c>Path</c>).</summary>
    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(maxAge: null));

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    private static CookieOptions Options(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        MaxAge = maxAge,
        IsEssential = true,
    };
}
