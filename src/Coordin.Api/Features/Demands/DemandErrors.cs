namespace Coordin.Api.Features.Demands;

/// <summary>Mensagens e códigos estáveis das regras de demanda (DT-18). Mensagens em português.</summary>
public static class DemandErrors
{
    public const string LockedCode = "completed-demand-locked";
    public const string LockedMessage = "A demanda já foi concluída e não pode mais ser alterada.";

    public const string PersonNotInTeamsCode = "person-not-in-demand-teams";
    public const string PersonNotInTeamsMessage = "A pessoa alocada precisa ser de um dos times da demanda.";

    public const string TitleRequired = "O título da demanda não pode ser vazio.";
    public const string TeamRepeated = "Um time não pode aparecer duas vezes na demanda.";
    public const string TeamMissing = "Informe o time de cada parte da demanda.";
    public const string TeamUnknown = "Um dos times informados não existe.";
    public const string EffortInvalid = "O esforço estimado deve ser maior que zero, com no máximo 2 casas decimais.";
    public const string PersonRepeated = "Uma pessoa não pode ser alocada duas vezes na mesma demanda.";
    public const string PersonUnknown = "Uma das pessoas informadas não existe.";
    public const string CompletionBeforeStart = "A data de conclusão não pode ser anterior ao início da demanda.";
    public const string CycleCode = "dependency-cycle";
    public const string DependentsCode = "demand-has-dependents";

    public const string PrerequisiteUnknown = "Uma das demandas que a demanda depende não existe.";
    public const string PrerequisiteRepeated = "Uma demanda não pode aparecer duas vezes como pré-requisito.";
    public const string PositionOutOfRange = "A posição deve estar entre 1 e o número de demandas mais 1.";
    public const string ReorderInvalid = "A lista precisa conter todas as demandas, uma única vez cada.";
}
