namespace Coordin.Api.Features.Validation;

/// <summary>
/// O planejamento do processo atual, guardado uma única vez como retrato serializado (PRD 7.3, passo 3; DT-37).
/// É um registro único (<see cref="SingletonId"/>): só um novo carregamento do conjunto o apaga.
/// </summary>
public sealed class ValidationCurrentPlan
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>O <c>PlanningSnapshot</c> serializado, na ordem de prioridade (ver <see cref="ValidationSnapshotJson"/>).</summary>
    public string SnapshotJson { get; set; } = "";

    public DateTimeOffset CapturedAt { get; set; }
}
