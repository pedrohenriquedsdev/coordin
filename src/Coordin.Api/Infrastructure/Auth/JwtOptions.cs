using System.Text;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Infrastructure.Auth;

/// <summary>
/// Configuração do token de acesso (DT-28). <c>Issuer</c> e <c>Audience</c> não são segredos e ficam
/// no <c>appsettings.json</c>; <c>SigningKey</c> vem só de variável de ambiente ou de
/// <c>dotnet user-secrets</c> (DT-30; RNF-05).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Tamanho mínimo da chave de assinatura, em bytes (DT-30).</summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; init; } = "";

    public string Audience { get; init; } = "";

    public string SigningKey { get; init; } = "";
}

/// <summary>
/// Faz a API recusar a inicialização com uma configuração de token insegura. As mensagens dizem
/// o que está errado, mas <b>nunca</b> repetem o valor da chave.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("'Jwt:Issuer' não está configurado.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("'Jwt:Audience' não está configurado.");
        }

        if (string.IsNullOrEmpty(options.SigningKey))
        {
            failures.Add(
                "'Jwt:SigningKey' não está configurada. Em desenvolvimento use 'dotnet user-secrets'; " +
                "em produção, a variável de ambiente Jwt__SigningKey.");
        }
        else if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"'Jwt:SigningKey' é curta demais: precisa de pelo menos {JwtOptions.MinimumSigningKeyBytes} bytes.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
