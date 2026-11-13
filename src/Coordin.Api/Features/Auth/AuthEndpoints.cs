using System.Security.Claims;
using Coordin.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Identity;

namespace Coordin.Api.Features.Auth;

/// <summary>
/// Endpoints de acesso do gestor (DT-28): <c>login</c>, <c>refresh</c>, <c>logout</c> e <c>me</c>.
/// Não existe endpoint de cadastro: a conta nasce da configuração (DT-29).
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Mensagem única para qualquer falha de login: não revela se o e-mail existe nem se a conta está bloqueada.</summary>
    private const string InvalidCredentials = "E-mail ou senha inválidos.";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Entra com e-mail e senha e devolve o token de acesso (o refresh token vai no cookie).")
            .Produces<AccessTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();

        // Sem metadado de autorização: herda a política padrão (exige o gestor autenticado).
        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Devolve os dados do gestor autenticado.")
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        UserManager<ApplicationUser> users,
        TokenService tokens,
        RefreshTokenService refreshTokens,
        CancellationToken cancellationToken)
    {
        // Os campos já foram validados como obrigatórios (400) antes de chegar aqui.
        var user = await users.FindByEmailAsync(request.Email!);
        if (user is null)
        {
            return ApiProblems.Unauthorized(InvalidCredentials);
        }

        // Conta bloqueada: nem confere a senha, e responde igual a uma senha errada.
        if (await users.IsLockedOutAsync(user))
        {
            return ApiProblems.Unauthorized(InvalidCredentials);
        }

        if (!await users.CheckPasswordAsync(user, request.Password!))
        {
            // Conta uma tentativa errada; na quinta o Identity bloqueia a conta por 15 minutos.
            await users.AccessFailedAsync(user);
            return ApiProblems.Unauthorized(InvalidCredentials);
        }

        // Um acerto zera a contagem: o bloqueio é só para erros consecutivos.
        await users.ResetAccessFailedCountAsync(user);

        var refreshToken = await refreshTokens.IssueAsync(user.Id, cancellationToken);
        RefreshTokenCookie.Append(http.Response, refreshToken);

        return Results.Ok(new AccessTokenResponse(tokens.CreateAccessToken(user)));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users)
    {
        // O 'sub' do token é o id do usuário; consultar o banco garante que a conta ainda existe.
        var userId = principal.FindFirstValue("sub");
        var user = userId is null ? null : await users.FindByIdAsync(userId);

        return user?.Email is { } email
            ? Results.Ok(new CurrentUserResponse(email))
            : ApiProblems.Unauthorized("É necessário entrar para acessar este recurso.");
    }
}
