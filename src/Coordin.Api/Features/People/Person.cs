using Coordin.Api.Features.Calendar;
using Coordin.Api.Features.Teams;

namespace Coordin.Api.Features.People;

/// <summary>
/// Pessoa de um time, com a capacidade em horas por dia útil (RF-02, DT-10). Capacidade zero é
/// válida e significa que a pessoa não contribui para as previsões.
/// </summary>
public sealed class Person
{
    public const int NameMaxLength = 100;
    public const decimal MaxDailyCapacityHours = 24m;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Name { get; set; } = "";

    public Guid TeamId { get; set; }

    public Team? Team { get; set; }

    /// <summary>Horas por dia útil (segunda a sexta), de 0 a 24, com até duas casas decimais.</summary>
    public decimal DailyCapacityHours { get; set; }

    public ICollection<Absence> Absences { get; } = [];
}
