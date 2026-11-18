namespace Coordin.Api.Features.Validation;

/// <summary>
/// A data que o gestor de desenvolvimento comunicaria ao gestor de produto para uma demanda, sem usar o sistema
/// (PRD 7.3, passo 2). Uma por demanda; apagada junto com a demanda e ao carregar o conjunto (DT-37).
/// </summary>
public sealed class ValidationBaseline
{
    public Guid DemandId { get; set; }

    public DateOnly BaselineDate { get; set; }
}
