using System.ComponentModel.DataAnnotations;

namespace Coordin.Api.Features.Calendar;

/// <summary>
/// Corpo de <c>POST /api/people/{id}/absences</c> e <c>PUT /api/absences/{id}</c> (RF-03). Tipo e datas
/// são anuláveis de propósito: um <c>DateOnly</c> ausente no corpo viraria <c>0001-01-01</c> sem erro.
/// Não existe campo de motivo (RNF-05); uma propriedade desconhecida no corpo é ignorada.
/// </summary>
public sealed record AbsenceRequest(
    [property: Required(ErrorMessage = "O tipo da ausência é obrigatório.")]
    AbsenceType? Type,
    [property: Required(ErrorMessage = "A data de início é obrigatória.")]
    DateOnly? StartDate,
    [property: Required(ErrorMessage = "A data de fim é obrigatória.")]
    DateOnly? EndDate) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is { } start && EndDate is { } end && end < start)
        {
            yield return new ValidationResult("O fim não pode ser anterior ao início.", [nameof(EndDate)]);
        }
    }
}

/// <summary>Uma ausência, com os dados da pessoa e do time para a tela do calendário não cruzar listas.</summary>
public sealed record AbsenceResponse(
    Guid Id,
    Guid PersonId,
    string PersonName,
    Guid TeamId,
    string TeamName,
    AbsenceType Type,
    DateOnly StartDate,
    DateOnly EndDate);

/// <summary>Corpo de <c>POST /api/holidays</c> (RF-03). Um feriado vale para todas as pessoas.</summary>
public sealed record HolidayRequest(
    [property: Required(ErrorMessage = "A data do feriado é obrigatória.")]
    DateOnly? Date,
    [property: StringLength(Holiday.DescriptionMaxLength, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")]
    string? Description);

public sealed record HolidayResponse(Guid Id, DateOnly Date, string? Description);
