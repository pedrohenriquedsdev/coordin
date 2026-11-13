namespace Coordin.Api.Features.Calendar;

/// <summary>
/// Tipo de uma ausência (RF-03). Só o tipo e o período são guardados, nunca o motivo (RNF-05).
/// Gravado como texto no banco: legível em consultas e estável se um valor novo for acrescentado.
/// </summary>
public enum AbsenceType
{
    Vacation,
    Other,
}
