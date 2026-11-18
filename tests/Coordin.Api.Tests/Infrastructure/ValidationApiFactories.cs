namespace Coordin.Api.Tests.Infrastructure;

/// <summary>API no ambiente <c>Testing</c> com a validação por simulação ligada (DT-37), para testar as rotas.</summary>
public sealed class ValidationEnabledApiFactory(PostgresFixture postgres)
    : CoordinApiFactory("Testing", postgres, configuration: new Dictionary<string, string?> { ["Validation:Enabled"] = "true" });

/// <summary>API no ambiente <c>Development</c> com a validação ligada, para conferir a documentação OpenAPI.</summary>
public sealed class ValidationEnabledDevelopmentFactory(PostgresFixture postgres)
    : CoordinApiFactory("Development", postgres, configuration: new Dictionary<string, string?> { ["Validation:Enabled"] = "true" });
