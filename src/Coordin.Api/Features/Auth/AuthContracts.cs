using System.ComponentModel.DataAnnotations;

namespace Coordin.Api.Features.Auth;

/// <summary>Corpo de <c>POST /api/auth/login</c>.</summary>
public sealed record LoginRequest(
    [property: Required(ErrorMessage = "O e-mail é obrigatório.")] string? Email,
    [property: Required(ErrorMessage = "A senha é obrigatória.")] string? Password);

/// <summary>
/// Resposta de <c>login</c> e <c>refresh</c>: só o token de acesso (JWT de 15 minutos). O refresh
/// token vai exclusivamente no cookie <c>HttpOnly</c>, nunca no corpo (DT-28).
/// </summary>
public sealed record AccessTokenResponse(string AccessToken);

/// <summary>Resposta de <c>GET /api/auth/me</c>: só o e-mail, sem hash de senha nem campos internos (RNF-05).</summary>
public sealed record CurrentUserResponse(string Email);
