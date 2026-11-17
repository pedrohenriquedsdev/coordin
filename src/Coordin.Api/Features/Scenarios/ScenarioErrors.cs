using Coordin.Engine.Planning;

namespace Coordin.Api.Features.Scenarios;

/// <summary>Código estável e mensagem dos erros dos cenários (DT-18). Mensagens em português.</summary>
public static class ScenarioErrors
{
    public const string ViolatesDependenciesCode = "scenario-violates-dependencies";

    public const string ViolatesDependenciesMessage =
        "A ordem aprovada deixaria uma demanda começar antes do fim previsto de um pré-requisito. Nada foi alterado.";

    /// <summary>
    /// O erro para as violações encontradas na conferência final de <c>apply</c>: <c>null</c> quando não há
    /// nenhuma, senão o código estável e a mensagem. Separado da resposta HTTP para ser testado sem servidor.
    /// </summary>
    public static (string Code, string Message)? Describe(IReadOnlyList<DependencyInput> violations) =>
        violations.Count == 0 ? null : (ViolatesDependenciesCode, ViolatesDependenciesMessage);
}
