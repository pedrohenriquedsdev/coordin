namespace Coordin.Api.Features.Validation;

/// <summary>
/// A área de validação por simulação (DT-37). Desligada por padrão: com <see cref="Enabled"/> falso, as rotas de
/// <c>/api/validation</c> não existem e respondem <c>404</c>. Ligá-la é decisão do autor, só durante a validação (RT-11).
/// </summary>
public sealed class ValidationOptions
{
    public const string SectionName = "Validation";

    public bool Enabled { get; init; }
}
