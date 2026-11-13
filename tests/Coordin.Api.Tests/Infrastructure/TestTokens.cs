using System.Security.Claims;
using System.Text;
using Coordin.Api.Infrastructure.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>
/// Fabrica tokens de acesso com defeitos escolhidos (expirado, chave errada, outro emissor...)
/// usando a mesma chave da aplicação. Assim não é preciso adiantar relógios para testar o validador.
/// </summary>
public static class TestTokens
{
    public static string Create(
        JwtOptions jwt,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTime.UtcNow;
        var expiresAt = expires ?? now.AddMinutes(15);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? jwt.Issuer,
            Audience = audience ?? jwt.Audience,
            IssuedAt = expiresAt.AddMinutes(-15),
            NotBefore = expiresAt.AddMinutes(-15),
            Expires = expiresAt,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, "usuario-de-teste"),
                new Claim(JwtRegisteredClaimNames.Email, TestManager.Email),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? jwt.SigningKey)),
                algorithm),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
