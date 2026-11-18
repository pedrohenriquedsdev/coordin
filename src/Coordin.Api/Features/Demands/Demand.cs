using Coordin.Api.Features.Planning;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Demanda do setor (RF-04 a RF-07, RF-12; TDD 4.2). Os rankings de prioridade e de execução são
/// únicos e contínuos a partir de 1. Sem <see cref="StartDate"/> ou sem estimativa de um time, a
/// demanda é não previsível (RF-10), mas pode ser salva (regra inviolável 5).
/// </summary>
public sealed class Demand
{
    public const int TitleMaxLength = 200;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Title { get; set; } = "";

    public int PriorityRank { get; set; }

    public int ExecutionRank { get; set; }

    /// <summary>Data de início planejada; obrigatória para a demanda receber previsão (RF-07).</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Prazo desejado ou combinado pelo gestor; não altera o cálculo (RF-07).</summary>
    public DateOnly? InformedEndDate { get; set; }

    /// <summary>Data real de conclusão; quando preenchida, a demanda fica travada (RF-12).</summary>
    public DateOnly? CompletedOn { get; set; }

    /// <summary>Esforço real gasto na demanda como um todo, informado na conclusão (RF-22); opcional.</summary>
    public decimal? ActualEffortHours { get; set; }

    /// <summary>A data prevista vigente no momento da conclusão (RF-23); gravada uma vez, na conclusão.</summary>
    public DateOnly? ForecastEndDateAtCompletion { get; set; }

    /// <summary>A soma das estimativas dos times no momento da conclusão (RF-22); gravada uma vez, na conclusão.</summary>
    public decimal? EstimatedEffortHoursAtCompletion { get; set; }

    public ICollection<DemandTeam> Teams { get; } = [];

    public ICollection<DemandAssignment> Assignments { get; } = [];

    /// <summary>As arestas em que esta demanda é a que espera (os pré-requisitos dela).</summary>
    public ICollection<DemandDependency> Prerequisites { get; } = [];

    /// <summary>As arestas em que esta demanda é o pré-requisito (quem espera por ela).</summary>
    public ICollection<DemandDependency> Dependents { get; } = [];

    public Forecast? Forecast { get; set; }

    /// <summary>Só o carregamento do conjunto fictício usa: fixa o id vindo do arquivo (DT-37).</summary>
    internal void UseId(Guid id) => Id = id;
}
