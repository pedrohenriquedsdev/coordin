namespace Coordin.Api.Features.Calendar;

/// <summary>
/// Feriado do setor (RF-03): em uma data de feriado ninguém contribui com capacidade. A data é uma
/// data de calendário (<see cref="DateOnly"/>) e é única.
/// </summary>
public sealed class Holiday
{
    public const int DescriptionMaxLength = 200;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public DateOnly Date { get; set; }

    public string? Description { get; set; }
}
