namespace Coordin.Api.Features.Demands;

/// <summary>
/// Uma mudança na data prevista de uma demanda (RF-21; DT-14). Somente inclusão: o banco recusa atualização e
/// exclusão enquanto a demanda existir. <see cref="Cause"/> é o nome de <c>ForecastChangeCause</c>.
/// </summary>
public sealed class ForecastHistory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid DemandId { get; set; }

    /// <summary>Nula na primeira previsão.</summary>
    public DateOnly? PreviousEndDate { get; set; }

    /// <summary>Nula quando a demanda deixou de ser previsível.</summary>
    public DateOnly? NewEndDate { get; set; }

    public string Cause { get; set; } = "";

    public DateTimeOffset ChangedAt { get; set; }
}

/// <summary>
/// Uma alteração na estimativa de um time em uma demanda (RF-20; DT-14). Somente inclusão. Sem chave estrangeira
/// para o time: o registro guarda o fato, e a exclusão de um time não pode ser travada por histórico.
/// </summary>
public sealed class EstimateHistory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid DemandId { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>Nula quando a estimativa não existia antes.</summary>
    public decimal? PreviousHours { get; set; }

    /// <summary>Nula quando a estimativa foi retirada.</summary>
    public decimal? NewHours { get; set; }

    /// <summary>O motivo informado pelo gestor, se houver (RF-20).</summary>
    public string? Reason { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
