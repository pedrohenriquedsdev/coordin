using Coordin.Api.Features.People;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Uma pessoa alocada a uma demanda, sem percentual (RF-05; DT-12). O time da pessoa precisa estar
/// entre os times da demanda; essa regra é validada pela API, não por esta tabela.
/// </summary>
public sealed class DemandAssignment
{
    public Guid DemandId { get; set; }

    public Guid PersonId { get; set; }

    public Demand? Demand { get; set; }

    public Person? Person { get; set; }
}
