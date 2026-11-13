using System.Security.Claims;
using System.Text;
using Coordin.Api.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Coordin.Api.Features.Auth;

/// <summary>
/// Emite o token de acesso (DT-28): JWT assinado com HMAC-SHA256, claims <c>sub</c> e <c>email</c>,
/// válido por 15 minutos. Quem valida é o JwtBearer, com a mesma chave (ver <see cref="AuthenticationSetup"/>).
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public string CreateAccessToken(ApplicationUser user)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(AccessTokenLifetime),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
