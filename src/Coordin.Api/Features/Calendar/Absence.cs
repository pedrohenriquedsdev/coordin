using Coordin.Api.Features.People;

namespace Coordin.Api.Features.Calendar;

/// <summary>
/// Ausência de uma pessoa (RF-03): durante o período ela não contribui com capacidade. As datas são
/// datas de calendário (<see cref="DateOnly"/>), sem fuso (DT-15). Não existe campo de motivo (RNF-05).
/// </summary>
public sealed class Absence
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid PersonId { get; set; }

    public Person? Person { get; set; }

    public AbsenceType Type { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Último dia da ausência, inclusive; nunca anterior ao início.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Só o carregamento do conjunto fictício usa: fixa o id vindo do arquivo (DT-37).</summary>
    internal void UseId(Guid id) => Id = id;
}
