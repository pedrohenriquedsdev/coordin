using System.ComponentModel.DataAnnotations;
using Coordin.Api.Infrastructure.Http;

namespace Coordin.Api.Features.People;

/// <summary>
/// Corpo de <c>POST /api/people</c> e <c>PUT /api/people/{id}</c> (RF-02). O time e a capacidade são
/// anuláveis de propósito: um valor ausente precisa virar erro de campo obrigatório, e não um valor
/// padrão (capacidade zero é válida e significa que a pessoa não contribui).
/// </summary>
public sealed record PersonRequest(
    [property: Required(ErrorMessage = "O nome da pessoa é obrigatório.")]
    [property: StringLength(Person.NameMaxLength, ErrorMessage = "O nome da pessoa deve ter no máximo 100 caracteres.")]
    string? Name,
    [property: Required(ErrorMessage = "O time é obrigatório.")]
    Guid? TeamId,
    [property: Required(ErrorMessage = "A capacidade em horas por dia útil é obrigatória.")]
    [property: Range(typeof(decimal), "0", "24", ErrorMessage = "A capacidade deve estar entre 0 e 24 horas por dia útil.")]
    [property: MaxDecimalPlaces(2, ErrorMessage = "A capacidade deve ter no máximo 2 casas decimais.")]
    decimal? DailyCapacityHours);

/// <summary>Uma pessoa, com o nome do time para a tela não precisar cruzar as listas.</summary>
public sealed record PersonResponse(Guid Id, string Name, Guid TeamId, string TeamName, decimal DailyCapacityHours);
