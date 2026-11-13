using System.Security.Cryptography;
using Coordin.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;

namespace Coordin.Api.Features.Auth;

/// <summary>
/// Emite refresh tokens (DT-28): 256 bits aleatórios, entregues ao cliente só uma vez e guardados
/// no banco apenas como hash SHA-256. Quem lê o banco não consegue usar nenhum token.
/// </summary>
public sealed class RefreshTokenService(CoordinDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private const int TokenBytes = 32;

    /// <summary>Cria um refresh token para o usuário e devolve o valor em texto, que só existe agora.</summary>
    public async Task<IssuedRefreshToken> IssueAsync(string userId, CancellationToken cancellationToken)
    {
        var value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        var now = clock.GetUtcNow();

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(value),
            ExpiresAt = now.Add(Lifetime),
        };

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(value, token.Id, token.ExpiresAt);
    }

    /// <summary>SHA-256 em hexadecimal minúsculo (64 caracteres): a "chave de busca" do cookie.</summary>
    public static string Hash(string tokenValue) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tokenValue)));
}

/// <summary>Refresh token recém-emitido: o <paramref name="Value"/> em texto só existe neste momento.</summary>
public sealed record IssuedRefreshToken(string Value, Guid Id, DateTimeOffset ExpiresAt);
