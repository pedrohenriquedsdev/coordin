using System.Security.Cryptography;
using System.Text;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Features.Auth;

/// <summary>
/// Refresh tokens (DT-28): 256 bits aleatórios, entregues ao cliente só uma vez e guardados no
/// banco apenas como hash SHA-256. Quem lê o banco não consegue usar nenhum token. A cada renovação
/// o token é trocado por outro; se um token já trocado reaparece, todos os tokens do usuário caem.
/// </summary>
public sealed class RefreshTokenService(CoordinDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private const int TokenBytes = 32;

    /// <summary>Cria um refresh token para o usuário e devolve o valor em texto, que só existe agora.</summary>
    public async Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken)
    {
        var token = NewToken(userId, out var value);
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(value, token.Id, token.ExpiresAt);
    }

    /// <summary>
    /// Troca o refresh token apresentado por um novo (rotação). O token antigo é marcado como
    /// revogado e aponta para o substituto. Se o token já tinha sido trocado, trata-se de reuso:
    /// todos os tokens do usuário são revogados.
    /// </summary>
    public async Task<RotationResult> RotateAsync(string presentedValue, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(presentedValue);
        var current = await db.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (current is null)
        {
            return RotationResult.Failed(RotationOutcome.Unknown);
        }

        if (current.ReplacedById is not null)
        {
            // Um token já trocado voltou: dois portadores da mesma sessão. Derruba tudo.
            await RevokeAllAsync(current.UserId, cancellationToken);
            return RotationResult.Failed(RotationOutcome.ReuseDetected);
        }

        if (current.RevokedAt is not null)
        {
            // Revogado por logout (ou revogação em massa): só deixa de valer, sem derrubar as demais sessões.
            return RotationResult.Failed(RotationOutcome.Revoked);
        }

        if (current.ExpiresAt <= now)
        {
            return RotationResult.Failed(RotationOutcome.Expired);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var replacement = NewToken(current.UserId, out var replacementValue);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);

        // O ponto crítico: "pegar" o token antigo é um UPDATE condicional e atômico. Se duas
        // chamadas chegam juntas com o mesmo cookie, só uma encontra RevokedAt ainda vazio.
        var claimed = await db.RefreshTokens
            .Where(token => token.Id == current.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.ReplacedById, replacement.Id),
                cancellationToken);

        if (claimed == 0)
        {
            // Perdeu a corrida: outra chamada já trocou este token. Desfaz o novo e trata como reuso.
            await transaction.RollbackAsync(cancellationToken);
            await RevokeAllAsync(current.UserId, cancellationToken);
            return RotationResult.Failed(RotationOutcome.ReuseDetected);
        }

        await transaction.CommitAsync(cancellationToken);

        return RotationResult.Rotated(
            new IssuedRefreshToken(replacementValue, replacement.Id, replacement.ExpiresAt),
            current.UserId);
    }

    /// <summary>Revoga só o token apresentado (logout); desconhecido ou já revogado não é erro.</summary>
    public async Task RevokeAsync(string presentedValue, CancellationToken cancellationToken)
    {
        var hash = Hash(presentedValue);
        var now = clock.GetUtcNow();

        await db.RefreshTokens
            .Where(token => token.TokenHash == hash && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    /// <summary>Revoga todos os tokens ativos do usuário (reuso detectado).</summary>
    public async Task RevokeAllAsync(string userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    /// <summary>SHA-256 em hexadecimal minúsculo (64 caracteres): a "chave de busca" do cookie.</summary>
    public static string Hash(string tokenValue) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tokenValue)));

    private RefreshToken NewToken(string userId, out string value)
    {
        value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(value),
            ExpiresAt = clock.GetUtcNow().Add(Lifetime),
        };
    }
}

/// <summary>Refresh token recém-emitido: o <paramref name="Value"/> em texto só existe neste momento.</summary>
public sealed record IssuedRefreshToken(string Value, Guid Id, DateTimeOffset ExpiresAt);

/// <summary>Por que uma renovação foi aceita ou recusada.</summary>
public enum RotationOutcome
{
    Rotated,
    Unknown,
    Revoked,
    Expired,
    ReuseDetected,
}

/// <summary>Resultado de <see cref="RefreshTokenService.RotateAsync"/>.</summary>
public sealed record RotationResult(RotationOutcome Outcome, IssuedRefreshToken? Token, string? UserId)
{
    public bool Succeeded => Outcome == RotationOutcome.Rotated;

    public static RotationResult Rotated(IssuedRefreshToken token, string userId) =>
        new(RotationOutcome.Rotated, token, userId);

    public static RotationResult Failed(RotationOutcome outcome) => new(outcome, null, null);
}
