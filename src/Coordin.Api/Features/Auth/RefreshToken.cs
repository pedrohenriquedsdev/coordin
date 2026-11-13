namespace Coordin.Api.Features.Auth;

/// <summary>
/// Refresh token de uma sessão do gestor (DT-28). Só o hash SHA-256 é guardado: quem lê o banco
/// não consegue usar o token. A cada renovação o token é trocado por outro (<see cref="ReplacedById"/>);
/// se um token já trocado reaparece, todos os tokens do usuário são revogados.
/// </summary>
public sealed class RefreshToken
{
    /// <summary>Tamanho do hash SHA-256 escrito em hexadecimal.</summary>
    public const int TokenHashLength = 64;

    public Guid Id { get; set; }

    public required string UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Preenchido quando o token deixa de valer (troca, logout ou revogação em massa).</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Token que o substituiu na rotação; vazio se foi revogado por logout ou em massa.</summary>
    public Guid? ReplacedById { get; set; }
}
