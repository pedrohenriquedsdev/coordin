using Coordin.Api.Infrastructure.Auth;
using Coordin.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Coordin.Api.Tests.Auth;

/// <summary>Spec authentication: segredo de assinatura obrigatório e fora do repositório (DT-30, RNF-05).</summary>
public class JwtConfigurationTests(PostgresFixture postgres)
{
    private static readonly string ValidKey = new('k', JwtOptions.MinimumSigningKeyBytes);

    private static JwtOptions Options(string? signingKey, string issuer = "coordin", string audience = "coordin-web") =>
        new() { Issuer = issuer, Audience = audience, SigningKey = signingKey ?? "" };

    [Fact]
    public void Validator_AcceptsKeyWithMinimumLength()
    {
        var result = new JwtOptionsValidator().Validate(null, Options(ValidKey));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validator_RejectsMissingKey()
    {
        var result = new JwtOptionsValidator().Validate(null, Options(null));

        Assert.True(result.Failed);
        Assert.Contains("Jwt:SigningKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_RejectsShortKey_WithoutRepeatingIt()
    {
        var shortKey = "chave-curta-secreta"; // 19 bytes

        var result = new JwtOptionsValidator().Validate(null, Options(shortKey));

        Assert.True(result.Failed);
        Assert.DoesNotContain(shortKey, result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_CountsBytes_NotCharacters()
    {
        // 16 caracteres, mas 32 bytes em UTF-8: vale. 15 caracteres de 2 bytes = 30 bytes: não vale.
        Assert.True(new JwtOptionsValidator().Validate(null, Options(new string('é', 16))).Succeeded);
        Assert.True(new JwtOptionsValidator().Validate(null, Options(new string('é', 15))).Failed);
    }

    [Fact]
    public void Validator_RejectsMissingIssuerOrAudience()
    {
        Assert.True(new JwtOptionsValidator().Validate(null, Options(ValidKey, issuer: "")).Failed);
        Assert.True(new JwtOptionsValidator().Validate(null, Options(ValidKey, audience: " ")).Failed);
    }

    [Fact]
    public async Task Api_WithoutSigningKey_RefusesToStart()
    {
        await using var factory = await FactoryWithKeyAsync(null);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:SigningKey", Flatten(error), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_WithShortSigningKey_RefusesToStart_WithoutLeakingTheValue()
    {
        var shortKey = "valor-secreto-curto";
        await using var factory = await FactoryWithKeyAsync(shortKey);

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var text = Flatten(error);
        Assert.Contains("Jwt:SigningKey", text, StringComparison.Ordinal);
        Assert.DoesNotContain(shortKey, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Api_WithValidSigningKey_Starts()
    {
        await using var factory = await FactoryWithKeyAsync(ValidKey);

        using var client = factory.CreateClient();
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

        Assert.Equal("coordin", options.Issuer);
        Assert.Equal("coordin-web", options.Audience);
    }

    private async Task<CoordinApiFactory> FactoryWithKeyAsync(string? signingKey)
    {
        var factory = new CoordinApiFactory(
            "Testing",
            postgres,
            configuration: new Dictionary<string, string?> { ["Jwt:SigningKey"] = signingKey });
        await factory.InitializeAsync();
        return factory;
    }

    private static string Flatten(Exception exception)
    {
        var parts = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            parts.Add(current.Message);
            if (current is OptionsValidationException validation)
            {
                parts.AddRange(validation.Failures);
            }
        }

        return string.Join(" | ", parts);
    }
}
