namespace Coordin.Api.Tests.Infrastructure;

/// <summary>
/// Conta fictícia do gestor usada só pelos testes de integração. Não é uma credencial real e
/// nenhum ambiente a conhece; ela só existe nos bancos descartáveis criados pelos testes.
/// </summary>
public static class TestManager
{
    public const string Email = "gestor@coordin.test";

    public const string Password = "senha-de-teste-12345";
}
